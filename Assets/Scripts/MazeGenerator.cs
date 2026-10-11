using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class MazeGenerator : MonoBehaviour
{
    // [FormerlySerializedAs] keeps the values you already set in the Inspector
    // after the fields were renamed from Turkish to English.
    [Header("Maze Size")]
    [FormerlySerializedAs("genislik")] public int width = 25;
    [FormerlySerializedAs("uzunluk")] public int length = 25;

    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    [FormerlySerializedAs("kapiPrefab")] public GameObject doorPrefab;
    [FormerlySerializedAs("mesalePrefab")] public GameObject torchPrefab;

    [Header("Special Objects")]
    [FormerlySerializedAs("cikisKapisiPrefab")] public GameObject exitDoorPrefab;

    [Header("Settings")]
    [FormerlySerializedAs("kapiSayisi")] public int doorCount = 20;
    [FormerlySerializedAs("kapiMesafeSiniri")] public float minDoorDistance = 8f;
    [FormerlySerializedAs("mesaleYogunlugu")]
    [Range(0, 100)]
    public int torchDensity = 10;

    private int[,] mazeGrid;
    private readonly List<Vector2Int> pathCells = new List<Vector2Int>();

    void Start()
    {
        StartCoroutine(GenerateWhenRoomIsReady());
    }

    // The number of questions comes from the server, so the maze is built only after
    // the student has joined a room. Without a QuestionManager it is built right away.
    private IEnumerator GenerateWhenRoomIsReady()
    {
        while (QuestionManager.instance != null && !QuestionManager.instance.IsRoomReady)
        {
            yield return null;
        }

        GenerateMaze();
    }

    private void GenerateMaze()
    {
        if (width % 2 == 0) width++;
        if (length % 2 == 0) length++;

        mazeGrid = new int[width, length];

        // 1 = wall, 0 = path
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                mazeGrid[x, z] = 1;
            }
        }

        CarveMaze(1, 1);

        // Entrance and exit openings
        mazeGrid[1, 0] = 0;
        mazeGrid[width - 2, length - 1] = 0;

        OpenExtraPaths();

        BuildMaze();

        PlaceExitDoor();

        PlaceDoors();
    }

    // Recursive backtracker: carves a path through the grid, two cells at a time.
    private void CarveMaze(int startX, int startZ)
    {
        mazeGrid[startX, startZ] = 0;
        pathCells.Add(new Vector2Int(startX, startZ));

        int[] directionX = { 0, 0, 2, -2 };
        int[] directionZ = { 2, -2, 0, 0 };

        // Shuffle the order of the four directions
        List<int> directionOrder = new List<int> { 0, 1, 2, 3 };
        for (int i = 0; i < directionOrder.Count; i++)
        {
            int temp = directionOrder[i];
            int randomIndex = Random.Range(i, directionOrder.Count);
            directionOrder[i] = directionOrder[randomIndex];
            directionOrder[randomIndex] = temp;
        }

        foreach (int i in directionOrder)
        {
            int newX = startX + directionX[i];
            int newZ = startZ + directionZ[i];

            if (newX > 0 && newX < width - 1 && newZ > 0 && newZ < length - 1 && mazeGrid[newX, newZ] == 1)
            {
                mazeGrid[startX + directionX[i] / 2, startZ + directionZ[i] / 2] = 0;
                pathCells.Add(new Vector2Int(startX + directionX[i] / 2, startZ + directionZ[i] / 2));
                CarveMaze(newX, newZ);
            }
        }
    }

    // Knocks out some walls so the maze has loops and more than one route.
    private void OpenExtraPaths()
    {
        int wallsToRemove = (width * length) / 30;
        int removed = 0;
        int attemptsLeft = 5000;

        while (removed < wallsToRemove && attemptsLeft > 0)
        {
            attemptsLeft--;
            int x = Random.Range(1, width - 1);
            int z = Random.Range(1, length - 1);

            if (mazeGrid[x, z] == 1)
            {
                bool pathAbove = mazeGrid[x, z + 1] == 0;
                bool pathBelow = mazeGrid[x, z - 1] == 0;
                bool pathRight = mazeGrid[x + 1, z] == 0;
                bool pathLeft = mazeGrid[x - 1, z] == 0;

                bool horizontalConnection = pathLeft && pathRight && !pathAbove && !pathBelow;
                bool verticalConnection = pathAbove && pathBelow && !pathLeft && !pathRight;

                if (horizontalConnection || verticalConnection)
                {
                    if (!CreatesOpenArea(x, z))
                    {
                        mazeGrid[x, z] = 0;
                        pathCells.Add(new Vector2Int(x, z));
                        removed++;
                    }
                }
            }
        }
    }

    // True if opening this cell would create a 2x2 block of open floor.
    private bool CreatesOpenArea(int x, int z)
    {
        if (mazeGrid[x - 1, z] == 0 && mazeGrid[x, z - 1] == 0 && mazeGrid[x - 1, z - 1] == 0) return true;
        if (mazeGrid[x + 1, z] == 0 && mazeGrid[x, z - 1] == 0 && mazeGrid[x + 1, z - 1] == 0) return true;
        if (mazeGrid[x - 1, z] == 0 && mazeGrid[x, z + 1] == 0 && mazeGrid[x - 1, z + 1] == 0) return true;
        if (mazeGrid[x + 1, z] == 0 && mazeGrid[x, z + 1] == 0 && mazeGrid[x + 1, z + 1] == 0) return true;

        return false;
    }

    private void BuildMaze()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (mazeGrid[x, z] == 1)
                {
                    Vector3 wallPosition = new Vector3(x * 2f, 2f, z * 2f);
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, transform);
                }
                else
                {
                    Vector3 floorPosition = new Vector3(x * 2f, 0, z * 2f);
                    Instantiate(floorPrefab, floorPosition, Quaternion.identity, transform);

                    // --- SMART WALL TORCH SYSTEM ---
                    if (torchPrefab != null)
                    {
                        // Skip the entrance and exit areas
                        if ((x > 1 || z > 1) && (x < width - 2 || z < length - 2))
                        {
                            if (Random.Range(0, 100) < torchDensity)
                            {
                                bool wallAbove = (z + 1 < length) && mazeGrid[x, z + 1] == 1;
                                bool wallBelow = (z - 1 >= 0) && mazeGrid[x, z - 1] == 1;
                                bool wallRight = (x + 1 < width) && mazeGrid[x + 1, z] == 1;
                                bool wallLeft = (x - 1 >= 0) && mazeGrid[x - 1, z] == 1;

                                Vector3 torchPosition = Vector3.zero;
                                Quaternion torchRotation = Quaternion.identity;
                                bool attachedToWall = false;

                                float height = 1.8f;
                                float offset = 0.9f;

                                if (wallAbove)
                                {
                                    torchPosition = new Vector3(x * 2f, height, z * 2f + offset);
                                    torchRotation = Quaternion.Euler(0, 0, 0);
                                    attachedToWall = true;
                                }
                                else if (wallBelow)
                                {
                                    torchPosition = new Vector3(x * 2f, height, z * 2f - offset);
                                    torchRotation = Quaternion.Euler(0, 180, 0);
                                    attachedToWall = true;
                                }
                                else if (wallRight)
                                {
                                    torchPosition = new Vector3(x * 2f + offset, height, z * 2f);
                                    torchRotation = Quaternion.Euler(0, 90, 0);
                                    attachedToWall = true;
                                }
                                else if (wallLeft)
                                {
                                    torchPosition = new Vector3(x * 2f - offset, height, z * 2f);
                                    torchRotation = Quaternion.Euler(0, -90, 0);
                                    attachedToWall = true;
                                }

                                if (attachedToWall)
                                {
                                    Instantiate(torchPrefab, torchPosition, torchRotation, transform);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void PlaceExitDoor()
    {
        if (exitDoorPrefab != null)
        {
            Vector3 exitPosition = new Vector3((width - 2) * 2f, 0, (length - 1) * 2f);
            Quaternion exitRotation = Quaternion.Euler(0, 0, 0);
            Instantiate(exitDoorPrefab, exitPosition, exitRotation, transform);
        }
    }

    private void PlaceDoors()
    {
        // Keep the entrance and exit cells free of doors
        pathCells.Remove(new Vector2Int(1, 1));
        pathCells.Remove(new Vector2Int(1, 0));
        pathCells.Remove(new Vector2Int(width - 2, length - 1));
        pathCells.Remove(new Vector2Int(width - 2, length - 2));

        // DoorController scripts of the doors we place, used later to hand out questions
        List<DoorController> placedDoorControllers = new List<DoorController>();

        int placedDoorCount = 0;
        int attemptsLeft = 2500;

        List<Vector3> placedDoorPositions = new List<Vector3>();

        while (placedDoorCount < doorCount && pathCells.Count > 0 && attemptsLeft > 0)
        {
            attemptsLeft--;
            int randomIndex = Random.Range(0, pathCells.Count);
            Vector2Int chosenCell = pathCells[randomIndex];
            int x = chosenCell.x;
            int z = chosenCell.y;

            bool wallAbove = (z + 1 < length) && mazeGrid[x, z + 1] == 1;
            bool wallBelow = (z - 1 >= 0) && mazeGrid[x, z - 1] == 1;
            bool wallRight = (x + 1 < width) && mazeGrid[x + 1, z] == 1;
            bool wallLeft = (x - 1 >= 0) && mazeGrid[x - 1, z] == 1;

            bool verticalCorridor = wallLeft && wallRight && !wallAbove && !wallBelow;
            bool horizontalCorridor = wallAbove && wallBelow && !wallLeft && !wallRight;

            if (verticalCorridor || horizontalCorridor)
            {
                Vector3 doorPosition = new Vector3(x * 2f, 0, z * 2f);

                // Late in the search, relax the spacing rule so all doors can still be placed
                float requiredDistance = (attemptsLeft > 500) ? minDoorDistance : 2f;

                bool tooCloseToAnotherDoor = false;
                foreach (Vector3 existingDoor in placedDoorPositions)
                {
                    if (Vector3.Distance(doorPosition, existingDoor) < requiredDistance)
                    {
                        tooCloseToAnotherDoor = true;
                        break;
                    }
                }

                if (!tooCloseToAnotherDoor)
                {
                    Quaternion doorRotation = Quaternion.identity;
                    if (horizontalCorridor) doorRotation = Quaternion.Euler(0, 90, 0);

                    GameObject spawnedDoor = Instantiate(doorPrefab, doorPosition, doorRotation, transform);
                    DoorController doorController = spawnedDoor.GetComponentInChildren<DoorController>();
                    if (doorController != null)
                    {
                        placedDoorControllers.Add(doorController);
                    }

                    placedDoorPositions.Add(doorPosition);
                    placedDoorCount++;
                }

                pathCells.RemoveAt(randomIndex);
            }
        }

        // --- RANDOM QUESTION DISTRIBUTION ---
        // Only as many doors as there are questions get a question, the rest open freely.
        int questionsNeeded = (QuestionManager.instance != null)
            ? QuestionManager.instance.TotalDoorCount
            : placedDoorControllers.Count;

        // Shuffle the doors
        for (int i = 0; i < placedDoorControllers.Count; i++)
        {
            DoorController temp = placedDoorControllers[i];
            int randomSwap = Random.Range(i, placedDoorControllers.Count);
            placedDoorControllers[i] = placedDoorControllers[randomSwap];
            placedDoorControllers[randomSwap] = temp;
        }

        // The first doors in the shuffled list carry questions
        for (int i = 0; i < placedDoorControllers.Count; i++)
        {
            placedDoorControllers[i].hasQuestion = i < questionsNeeded;
        }
    }
}