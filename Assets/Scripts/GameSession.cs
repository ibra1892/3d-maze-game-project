// Static data survives scene changes, so the menu scene can hand the login and the
// chosen room over to the game scene.
public static class GameSession
{
    // Set by the main menu. If empty (game scene started directly), QuestionManager uses its own value.
    public static string ServerUrl = "";

    // Login token of the student (empty when nobody is logged in).
    public static string Token = "";

    // "First Last" of the logged-in student.
    public static string PlayerName = "";

    // Room the student chose in the menu.
    public static string RoomCode = "";

    public static bool IsLoggedIn
    {
        get { return !string.IsNullOrEmpty(Token); }
    }
}