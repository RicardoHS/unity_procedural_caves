using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/*
Tutorial: https://www.youtube.com/watch?v=v7yyZZjF1z4
Generate procedural caves in Unity using celular automata algorithm.
Smooth the generated cubic mesh borders with marching squares algorithm.
Add deep to the mesh by finding the outlines vertices of the mesh.
Diferenciate the different zones of the caves by using a flood fill algorithm.
*/

public class WorldGenerator : MonoBehaviour
{
    public int width;
    public int height;
    [Range(0, 100)]
    public int randomFillPercent;
    public string seed;
    public bool useRandomSeed;
    [Range(0, 10)]
    public int smoothIterations;
    public bool removeSmallRegions = true;
    public int wallRegionMinSize = 5;
    public int roomRegionMinSize = 20;
    public bool connectRegions = true;
    int[,] map;

    struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }

    class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccesableFromMainRoom;
        public bool isMainRoom;

        public Room()
        {

        }

        public Room(List<Coord> roomTiles, int[,] map)
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();

            foreach (Coord tile in tiles)
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                    for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                    {
                        if (x == tile.tileX || y == tile.tileY)
                            if (map[x, y] == 1)
                                edgeTiles.Add(tile);
                    }
        }

        public static void ConnectRooms(Room a, Room b)
        {
            if (a.isAccesableFromMainRoom)
            {
                b.SetAccesableFromMainRoom();
            }
            else if (b.isAccesableFromMainRoom)
            {
                a.SetAccesableFromMainRoom();
            }
            a.connectedRooms.Add(b);
            b.connectedRooms.Add(a);
        }

        public void SetAccesableFromMainRoom()
        {
            if (!isAccesableFromMainRoom)
            {
                isAccesableFromMainRoom = true;
                foreach (Room connectedRoom in connectedRooms)
                    connectedRoom.SetAccesableFromMainRoom();
            }
        }

        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GenerateMap();
        }
    }


    void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();

        for (int i = 0; i < smoothIterations; i++)
            SmoothMap();

        ProcessRegions();

        int borderSize = 5;
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

        for (int x = 0; x < borderedMap.GetLength(0); x++)
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1);
    }

    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = Time.time.ToString();
        }

        System.Random pseudoRand = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRand.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
    }

    void SmoothMap()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int neightboorWallTiles = GetSurroundingWallCount(x, y);
                if (neightboorWallTiles > 4)
                {
                    map[x, y] = 1;
                }
                else if (neightboorWallTiles < 4)
                {
                    map[x, y] = 0;
                }
            }
    }

    int GetSurroundingWallCount(int xGrid, int yGrid)
    {
        int wallCount = 0;
        for (int neighbourX = xGrid - 1; neighbourX <= xGrid + 1; neighbourX++)
            for (int neighbourY = yGrid - 1; neighbourY <= yGrid + 1; neighbourY++)
            {
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    if (neighbourX != xGrid || neighbourY != yGrid)
                        wallCount += map[neighbourX, neighbourY];
                }
                else
                {
                    wallCount++;
                }
            }
        return wallCount;
    }

    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    void ProcessRegions()
    {
        List<List<Coord>> wallRegions = GetRegions(1);
        if (removeSmallRegions)
            foreach (List<Coord> region in wallRegions)
                if (region.Count < wallRegionMinSize)
                    foreach (Coord tile in region)
                        map[tile.tileX, tile.tileY] = 0;

        List<List<Coord>> roomRegions = GetRegions(0);
        List<Room> survivingRooms = new List<Room>();
        foreach (List<Coord> region in roomRegions)
            if (region.Count < roomRegionMinSize && removeSmallRegions)
            {
                foreach (Coord tile in region)
                    map[tile.tileX, tile.tileY] = 1;
            }
            else
            {
                survivingRooms.Add(new Room(region, map));
            }

        if (connectRegions)
        {
            survivingRooms.Sort();
            survivingRooms[0].isMainRoom = true;
            survivingRooms[0].isAccesableFromMainRoom = true;
            ConnectClosedRooms(survivingRooms);
        }
    }

    void ConnectClosedRooms(List<Room> allRooms, bool forceAccesabilityFromMainRoom = false)
    {
        List<Room> listRoomA = new List<Room>();
        List<Room> listRoomB = new List<Room>();

        if (forceAccesabilityFromMainRoom)
        {

            foreach (Room room in allRooms)
                if (room.isAccesableFromMainRoom)
                {
                    listRoomB.Add(room);
                }
                else
                {
                    listRoomA.Add(room);
                }
        }
        else
        {
            listRoomA = allRooms;
            listRoomB = allRooms;
        }

        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in listRoomA)
        {
            if (!forceAccesabilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0)
                    continue;
            }

            foreach (Room roomB in listRoomB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB))
                    continue;

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
            }
            if (possibleConnectionFound && !forceAccesabilityFromMainRoom)
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
        }

        if (possibleConnectionFound && forceAccesabilityFromMainRoom)
        {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosedRooms(allRooms, true);
        }

        if (!forceAccesabilityFromMainRoom)
            ConnectClosedRooms(allRooms, true);
    }

    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);
        List<Coord> line = GetLine(tileA, tileB);
        foreach (Coord c in line)
            DrawCircle(c, 1);
    }

    void DrawCircle(Coord c, int r)
    {
        for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
                if (x * x + y * y <= r * r)
                {
                    int realX = c.tileX + x;
                    int realY = c.tileY + y;

                    if (IsInMapRange(realX, realY))
                        map[realX, realY] = 0;
                }
    }

    List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
                y += step;
            else
                x += step;

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                    x += gradientStep;
                else
                    y += gradientStep;
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    Vector3 CoordToWorldPoint(Coord tile)
    {
        return new Vector3(-width / 2 + .5f + tile.tileX, 2, -height / 2 + 0.5f + tile.tileY);
    }

    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTile(x, y);
                    regions.Add(newRegion);

                    foreach (Coord tile in newRegion)
                        mapFlags[tile.tileX, tile.tileY] = 1;
                }
            }

        return regions;
    }

    List<Coord> GetRegionTile(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);
            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                        if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                }
        }
        return tiles;
    }
}
