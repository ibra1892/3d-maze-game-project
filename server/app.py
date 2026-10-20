import base64
import hashlib
import hmac
import os
import re
import secrets
import smtplib
import threading
from datetime import datetime, timedelta, timezone
from email.message import EmailMessage
from functools import wraps

import jwt
import requests
from dotenv import load_dotenv
from flask import Flask, g, jsonify, render_template, request
from postgrest.exceptions import APIError
from supabase import create_client
from werkzeug.security import check_password_hash, generate_password_hash

load_dotenv()

SUPABASE_URL = os.environ["SUPABASE_URL"]
SUPABASE_SERVICE_KEY = os.environ["SUPABASE_SERVICE_KEY"]  # server only, never put in Unity
CLERK_PUBLISHABLE_KEY = os.environ["CLERK_PUBLISHABLE_KEY"]
CLERK_SECRET_KEY = os.environ["CLERK_SECRET_KEY"]
STUDENT_TOKEN_SECRET = os.environ["STUDENT_TOKEN_SECRET"]  # signs student login tokens

# The Clerk "frontend API" domain is encoded inside the publishable key.
CLERK_FRONTEND_API = (
    base64.b64decode(CLERK_PUBLISHABLE_KEY.split("_", 2)[2] + "==").decode().rstrip("$")
)
jwks_client = jwt.PyJWKClient(f"https://{CLERK_FRONTEND_API}/.well-known/jwks.json")

db = create_client(SUPABASE_URL, SUPABASE_SERVICE_KEY)
app = Flask(__name__)

# No 0/O, 1/I/L so codes are easy to read out loud in a classroom.
ROOM_CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"
ROOM_CODE_LENGTH = 6
DIFFICULTIES = {"easy", "medium", "hard"}
MIN_QUESTIONS = 1
MAX_QUESTIONS = 20

EMAIL_PATTERN = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")
STUDENT_TOKEN_DAYS = 30
# Used so a login for an unknown email takes as long as one for a known email.
DUMMY_PASSWORD_HASH = generate_password_hash("not-a-real-password")

# Password reset codes
RESET_CODE_MINUTES = 15       # how long a code works
RESET_MAX_ATTEMPTS = 5        # wrong guesses before the code is thrown away
RESET_COOLDOWN_SECONDS = 60   # minimum time between two code requests for the same account

# Email settings are optional. Without them the reset code is printed in this terminal instead
# of being emailed, which is handy for testing.
SMTP_HOST = os.environ.get("SMTP_HOST", "")
SMTP_PORT = int(os.environ.get("SMTP_PORT", "587"))
SMTP_USER = os.environ.get("SMTP_USER", "")
SMTP_PASSWORD = os.environ.get("SMTP_PASSWORD", "")
SMTP_FROM = os.environ.get("SMTP_FROM", "") or SMTP_USER


# ---------------------------------------------------------------- helpers

def error(message, status=400):
    return jsonify({"error": message}), status


def generate_room_code():
    return "".join(secrets.choice(ROOM_CODE_ALPHABET) for _ in range(ROOM_CODE_LENGTH))


def to_int(value, minimum, maximum, default=None):
    try:
        number = int(value)
    except (TypeError, ValueError):
        return default
    return number if minimum <= number <= maximum else default


_known_teachers = set()


def fetch_clerk_email(clerk_id):
    try:
        response = requests.get(
            f"https://api.clerk.com/v1/users/{clerk_id}",
            headers={"Authorization": f"Bearer {CLERK_SECRET_KEY}"},
            timeout=10,
        )
        response.raise_for_status()
        user = response.json()
        primary_id = user.get("primary_email_address_id")
        for item in user.get("email_addresses", []):
            if item["id"] == primary_id:
                return item["email_address"]
    except requests.RequestException:
        pass
    return f"{clerk_id}@unknown.invalid"


def ensure_teacher(clerk_id):
    """Create or update the users row the first time a teacher uses the API."""
    if clerk_id in _known_teachers:
        return
    db.table("users").upsert(
        {"clerk_id": clerk_id, "email": fetch_clerk_email(clerk_id), "role": "teacher"},
        on_conflict="clerk_id",
    ).execute()
    _known_teachers.add(clerk_id)


def require_teacher(view):
    @wraps(view)
    def wrapper(*args, **kwargs):
        header = request.headers.get("Authorization", "")
        if not header.startswith("Bearer "):
            return error("Missing token.", 401)
        token = header[len("Bearer "):]
        try:
            signing_key = jwks_client.get_signing_key_from_jwt(token)
            claims = jwt.decode(
                token,
                signing_key.key,
                algorithms=["RS256"],
                options={"verify_aud": False},
                leeway=10,
            )
        except jwt.PyJWTError:
            return error("Invalid or expired token.", 401)
        g.clerk_id = claims["sub"]
        ensure_teacher(g.clerk_id)
        return view(*args, **kwargs)

    return wrapper


def make_student_token(student_id):
    now = datetime.now(timezone.utc)
    claims = {
        "sub": str(student_id),
        "typ": "student",
        "iat": now,
        "exp": now + timedelta(days=STUDENT_TOKEN_DAYS),
    }
    return jwt.encode(claims, STUDENT_TOKEN_SECRET, algorithm="HS256")


def public_student(row):
    return {
        "first_name": row["first_name"],
        "last_name": row["last_name"],
        "email": row["email"],
    }


def require_student(view):
    @wraps(view)
    def wrapper(*args, **kwargs):
        header = request.headers.get("Authorization", "")
        if not header.startswith("Bearer "):
            return error("Missing token.", 401)
        try:
            claims = jwt.decode(
                header[len("Bearer "):], STUDENT_TOKEN_SECRET, algorithms=["HS256"], leeway=10
            )
            if claims.get("typ") != "student":
                raise jwt.InvalidTokenError("wrong token type")
            student_id = int(claims["sub"])
        except (jwt.PyJWTError, ValueError, KeyError):
            return error("Invalid or expired token.", 401)
        result = db.table("students").select("*").eq("id", student_id).limit(1).execute()
        if not result.data:
            return error("Account not found.", 401)
        g.student = result.data[0]
        return view(*args, **kwargs)

    return wrapper


def get_owned_room(room_code):
    result = (
        db.table("rooms")
        .select("*")
        .eq("room_code", room_code.upper())
        .eq("teacher_id", g.clerk_id)
        .limit(1)
        .execute()
    )
    return result.data[0] if result.data else None


def get_room(room_code):
    result = db.table("rooms").select("*").eq("room_code", room_code.upper()).limit(1).execute()
    return result.data[0] if result.data else None


def split_answers(raw):
    """Split "20, yirmi" into ["20", "yirmi"]. A "|" also separates answers.

    A comma with a digit on both sides ("1,4" or "2, 3") stays inside the answer, because it is
    part of a decimal number or a list such as a set. To give two numeric alternatives, use "|".
    """
    answers = []
    start = 0
    for index, character in enumerate(raw):
        if character not in ",|":
            continue
        if character == ",":
            before = raw[:index].rstrip()
            after = raw[index + 1:].lstrip()
            if before[-1:].isdigit() and after[:1].isdigit():
                continue
        answers.append(raw[start:index])
        start = index + 1
    answers.append(raw[start:])
    return [answer.strip() for answer in answers if answer.strip()]


def parse_question(data):
    question_text = (data.get("question_text") or "").strip()
    # The game stores several accepted answers separated by "|" and checks each one.
    answers = split_answers(data.get("correct_answer") or "")
    hint = (data.get("hint") or "").strip() or None
    difficulty = data.get("difficulty") or "medium"
    if not question_text or not answers:
        raise ValueError("Question and correct answer are required.")
    if difficulty not in DIFFICULTIES:
        raise ValueError("Difficulty must be easy, medium or hard.")
    return {
        "question_text": question_text,
        "correct_answer": "|".join(answers),
        "hint": hint,
        "difficulty": difficulty,
    }


# ---------------------------------------------------------------- teacher page

@app.get("/")
def teacher_page():
    return render_template(
        "teacher.html",
        clerk_publishable_key=CLERK_PUBLISHABLE_KEY,
        clerk_frontend_api=CLERK_FRONTEND_API,
    )


# ---------------------------------------------------------------- teacher API

@app.get("/api/rooms")
@require_teacher
def list_rooms():
    result = (
        db.table("rooms")
        .select("room_code, duration_minutes, question_count, created_at")
        .eq("teacher_id", g.clerk_id)
        .order("created_at", desc=True)
        .execute()
    )
    return jsonify({"rooms": result.data})


@app.post("/api/rooms")
@require_teacher
def create_room():
    data = request.get_json(silent=True) or {}
    question_count = to_int(data.get("question_count"), MIN_QUESTIONS, MAX_QUESTIONS)
    if question_count is None:
        return error(f"Number of questions must be between {MIN_QUESTIONS} and {MAX_QUESTIONS}.")
    duration = to_int(data.get("duration_minutes"), 1, 180, default=15)

    for _ in range(10):  # retry if a random code collides with an existing one
        room_code = generate_room_code()
        try:
            result = (
                db.table("rooms")
                .insert(
                    {
                        "room_code": room_code,
                        "teacher_id": g.clerk_id,
                        "duration_minutes": duration,
                        "question_count": question_count,
                    }
                )
                .execute()
            )
            return jsonify({"room": result.data[0]}), 201
        except APIError as exc:
            if getattr(exc, "code", None) != "23505":  # 23505 = unique violation
                raise
    return error("Could not generate a unique room code. Try again.", 500)


@app.delete("/api/rooms/<room_code>")
@require_teacher
def delete_room(room_code):
    room = get_owned_room(room_code)
    if not room:
        return error("Room not found.", 404)
    code = room["room_code"]
    # scores and questions reference the room, so they must be deleted first
    db.table("scores").delete().eq("room_code", code).execute()
    db.table("questions").delete().eq("room_code", code).execute()
    db.table("rooms").delete().eq("room_code", code).execute()
    return jsonify({"deleted": code})


@app.post("/api/rooms/<room_code>/questions")
@require_teacher
def add_question(room_code):
    room = get_owned_room(room_code)
    if not room:
        return error("Room not found.", 404)
    try:
        question = parse_question(request.get_json(silent=True) or {})
    except ValueError as exc:
        return error(str(exc))
    existing = (
        db.table("questions")
        .select("id", count="exact")
        .eq("room_code", room["room_code"])
        .execute()
        .count
    )
    if existing >= room["question_count"]:
        return error(f"This room is full. It was created for {room['question_count']} questions.")
    question["room_code"] = room["room_code"]
    result = db.table("questions").insert(question).execute()
    return jsonify({"question": result.data[0]}), 201


@app.delete("/api/rooms/<room_code>/questions/<int:question_id>")
@require_teacher
def delete_question(room_code, question_id):
    room = get_owned_room(room_code)
    if not room:
        return error("Room not found.", 404)
    db.table("questions").delete().eq("id", question_id).eq("room_code", room["room_code"]).execute()
    return jsonify({"deleted": question_id})


@app.get("/api/rooms/<room_code>/scores")
@require_teacher
def list_scores(room_code):
    room = get_owned_room(room_code)
    if not room:
        return error("Room not found.", 404)
    result = (
        db.table("scores")
        .select("player_name, duration_seconds, solved_question_count, total_question_count, won, completed_at")
        .eq("room_code", room["room_code"])
        .order("completed_at", desc=True)
        .execute()
    )
    return jsonify({"scores": result.data})


# ---------------------------------------------------------------- student accounts (Unity)

@app.post("/api/students/register")
def register_student():
    data = request.get_json(silent=True) or {}
    first_name = (data.get("first_name") or "").strip()[:40]
    last_name = (data.get("last_name") or "").strip()[:40]
    email = (data.get("email") or "").strip().lower()
    password = data.get("password") or ""

    if not first_name or not last_name:
        return error("First name and last name are required.")
    if len(email) > 254 or not EMAIL_PATTERN.match(email):
        return error("Enter a valid email address.")
    if not 8 <= len(password) <= 128:
        return error("Password must be between 8 and 128 characters.")
    if data.get("password_confirm") is not None and data["password_confirm"] != password:
        return error("The two passwords do not match.")

    try:
        result = (
            db.table("students")
            .insert(
                {
                    "first_name": first_name,
                    "last_name": last_name,
                    "email": email,
                    "password_hash": generate_password_hash(password),
                }
            )
            .execute()
        )
    except APIError as exc:
        if getattr(exc, "code", None) == "23505":  # unique violation: email already used
            return error("An account with this email already exists.", 409)
        raise

    student = result.data[0]
    return jsonify({"token": make_student_token(student["id"]), "student": public_student(student)}), 201


@app.post("/api/students/login")
def login_student():
    data = request.get_json(silent=True) or {}
    email = (data.get("email") or "").strip().lower()
    password = data.get("password") or ""

    result = db.table("students").select("*").eq("email", email).limit(1).execute()
    student = result.data[0] if result.data else None

    # Same work for unknown emails, and the same message, so emails cannot be guessed.
    password_ok = check_password_hash(student["password_hash"] if student else DUMMY_PASSWORD_HASH, password)
    if not student or not password_ok:
        return error("Wrong email or password.", 401)

    return jsonify({"token": make_student_token(student["id"]), "student": public_student(student)})


@app.get("/api/students/me")
@require_student
def student_profile():
    """Unity calls this on start-up to check that the saved login is still valid."""
    return jsonify({"student": public_student(g.student)})


# ---------------------------------------------------------------- password reset

def hash_reset_code(student_id, code):
    """Codes are stored only as a keyed hash, so a leaked database does not reveal them."""
    message = f"{student_id}:{code}".encode()
    return hmac.new(STUDENT_TOKEN_SECRET.encode(), message, hashlib.sha256).hexdigest()


def deliver_reset_code(email, code):
    body = (
        f"Your Maze Quiz password reset code is: {code}\n\n"
        f"It works for {RESET_CODE_MINUTES} minutes. "
        "If you did not ask for this, you can ignore this email."
    )

    if not SMTP_HOST or not SMTP_USER or not SMTP_PASSWORD:
        print(f"[EMAIL NOT CONFIGURED] Reset code for {email}: {code}")
        return

    try:
        message = EmailMessage()
        message["Subject"] = "Your Maze Quiz password reset code"
        message["From"] = SMTP_FROM
        message["To"] = email
        message.set_content(body)

        with smtplib.SMTP(SMTP_HOST, SMTP_PORT, timeout=15) as server:
            server.starttls()
            server.login(SMTP_USER, SMTP_PASSWORD)
            server.send_message(message)
    except Exception as exc:  # the student is told nothing, so the error is only shown here
        print(f"[EMAIL ERROR] Could not send the reset code to {email}: {exc}")


@app.post("/api/students/forgot-password")
def forgot_password():
    data = request.get_json(silent=True) or {}
    email = (data.get("email") or "").strip().lower()
    if len(email) > 254 or not EMAIL_PATTERN.match(email):
        return error("Enter a valid email address.")

    result = db.table("students").select("id, reset_requested_at").eq("email", email).limit(1).execute()
    if result.data:
        student = result.data[0]
        now = datetime.now(timezone.utc)

        last_request = student.get("reset_requested_at")
        asked_recently = bool(last_request) and (
            now - datetime.fromisoformat(last_request)
        ).total_seconds() < RESET_COOLDOWN_SECONDS

        if not asked_recently:
            code = f"{secrets.randbelow(10 ** 6):06d}"
            db.table("students").update(
                {
                    "reset_code_hash": hash_reset_code(student["id"], code),
                    "reset_expires_at": (now + timedelta(minutes=RESET_CODE_MINUTES)).isoformat(),
                    "reset_attempts": 0,
                    "reset_requested_at": now.isoformat(),
                }
            ).eq("id", student["id"]).execute()

            # Sent in the background so the answer takes the same time whether or not the account exists
            threading.Thread(target=deliver_reset_code, args=(email, code), daemon=True).start()

    # Same answer for every valid email, so nobody can find out who has an account
    return jsonify({"message": "If an account with this email exists, a reset code has been sent."})


@app.post("/api/students/reset-password")
def reset_password():
    data = request.get_json(silent=True) or {}
    email = (data.get("email") or "").strip().lower()
    code = (data.get("code") or "").strip()
    password = data.get("password") or ""

    if not 8 <= len(password) <= 128:
        return error("Password must be between 8 and 128 characters.")
    if data.get("password_confirm") is not None and data["password_confirm"] != password:
        return error("The two passwords do not match.")

    invalid_code = error("Invalid or expired code.")

    result = (
        db.table("students")
        .select("id, reset_code_hash, reset_expires_at, reset_attempts")
        .eq("email", email)
        .limit(1)
        .execute()
    )
    if not result.data:
        return invalid_code
    student = result.data[0]

    if not student["reset_code_hash"] or not student["reset_expires_at"]:
        return invalid_code
    if datetime.now(timezone.utc) > datetime.fromisoformat(student["reset_expires_at"]):
        return invalid_code
    if student["reset_attempts"] >= RESET_MAX_ATTEMPTS:
        return invalid_code

    if not hmac.compare_digest(student["reset_code_hash"], hash_reset_code(student["id"], code)):
        db.table("students").update({"reset_attempts": student["reset_attempts"] + 1}).eq("id", student["id"]).execute()
        return invalid_code

    db.table("students").update(
        {
            "password_hash": generate_password_hash(password),
            "reset_code_hash": None,
            "reset_expires_at": None,
            "reset_attempts": 0,
        }
    ).eq("id", student["id"]).execute()
    return jsonify({"message": "Password changed. You can now log in."})


# ---------------------------------------------------------------- public API (Unity)

@app.get("/api/rooms/<room_code>/questions")
def get_questions(room_code):
    """Students enter a room code in Unity; this returns the room's questions."""
    room = get_room(room_code)
    if not room:
        return error("Room not found.", 404)
    result = (
        db.table("questions")
        .select("id, question_text, correct_answer, hint, difficulty")
        .eq("room_code", room["room_code"])
        .order("id")
        .execute()
    )
    return jsonify(
        {
            "room_code": room["room_code"],
            "duration_minutes": room["duration_minutes"],
            "question_count": room["question_count"],
            "questions": result.data,
        }
    )


@app.post("/api/rooms/<room_code>/scores")
@require_student
def submit_score(room_code):
    room = get_room(room_code)
    if not room:
        return error("Room not found.", 404)
    data = request.get_json(silent=True) or {}

    duration_seconds = to_int(data.get("duration_seconds"), 0, 86400)
    solved = to_int(data.get("solved_question_count"), 0, 1000)
    total = to_int(data.get("total_question_count"), 0, 1000)
    if None in (duration_seconds, solved, total) or solved > total:
        return error("Invalid score data.")

    result = (
        db.table("scores")
        .insert(
            {
                "room_code": room["room_code"],
                "student_id": g.student["id"],
                # The name comes from the account, not from the game, so it cannot be faked.
                "player_name": f"{g.student['first_name']} {g.student['last_name']}",
                "duration_seconds": duration_seconds,
                "solved_question_count": solved,
                "total_question_count": total,
                "won": bool(data.get("won")),
            }
        )
        .execute()
    )
    return jsonify({"score": result.data[0]}), 201


@app.get("/api/rooms/<room_code>/leaderboard")
def leaderboard(room_code):
    """Best result of each player in this room: escaped first, then most solved, then fastest."""
    room = get_room(room_code)
    if not room:
        return error("Room not found.", 404)

    rows = (
        db.table("scores")
        .select("student_id, player_name, duration_seconds, solved_question_count, total_question_count, won")
        .eq("room_code", room["room_code"])
        .execute()
        .data
    )

    best = {}
    for row in rows:
        player_key = row["student_id"] if row["student_id"] is not None else row["player_name"]
        sort_key = (not row["won"], -row["solved_question_count"], row["duration_seconds"])
        if player_key not in best or sort_key < best[player_key][0]:
            best[player_key] = (sort_key, row)

    ranked = sorted(best.values(), key=lambda item: item[0])[:20]
    entries = [
        {
            "rank": position,
            "player_name": row["player_name"],
            "solved_question_count": row["solved_question_count"],
            "total_question_count": row["total_question_count"],
            "duration_seconds": row["duration_seconds"],
            "won": bool(row["won"]),
        }
        for position, (_, row) in enumerate(ranked, start=1)
    ]
    return jsonify({"room_code": room["room_code"], "entries": entries})


if __name__ == "__main__":
    app.run(debug=True, port=5000)