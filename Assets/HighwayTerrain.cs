using UnityEngine;

// ============================================================================
// HIGHWAY TERRAIN - Generates hilly terrain that respects the highway path
// ============================================================================
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class HighwayTerrain : MonoBehaviour
{
    [Header("References")]
    public MeshCollider highwayCollider;

    [Header("Terrain Size")]
    [Range(10f, 200f)] public float terrainPadding = 50f; // Extra space around highway
    [Range(0.5f, 5f)] public float gridResolution = 2f; // Distance between vertices

    [Header("Height Settings")]
    [Range(0f, 50f)] public float baseTerrainHeight = 5f;
    [Range(0f, 30f)] public float hillHeight = 15f;
    [Range(0.01f, 0.2f)] public float hillFrequency = 0.05f; // Lower = larger hills
    [Range(1, 4)] public int noiseOctaves = 3;
    [Range(0f, 1f)] public float noisePersistence = 0.5f;

    [Header("Road Flattening")]
    [Range(5f, 50f)] public float flattenDistance = 20f; // Distance from road to flatten
    [Range(0.1f, 5f)] public float flattenFalloff = 2f; // How gradually it transitions
    [Range(-5f, 5f)] public float roadHeightOffset = 0f; // Raise/lower road relative to terrain

    [Header("Terrain Features")]
    [Range(0f, 2f)] public float edgeFalloff = 0.5f; // Terrain drops at edges
    public Vector2 noiseOffset = Vector2.zero; // Randomize terrain

    [Header("Materials")]
    public Material terrainMaterial;

    [Header("Performance")]
    public bool autoUpdate = true;
    public bool generateCollider = true;
    [Range(0.5f, 10f)] public float minGridResolution = 2f; // Don't go below this for performance

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh terrainMesh;

    private Bounds highwayBounds;
    private Vector3 terrainCenter;
    private float terrainWidth;
    private float terrainLength;

    // Cache highway sample points for faster distance checks
    private Vector3[] highwaySamplePoints;
    private const int HIGHWAY_SAMPLES = 100;

    void Start()
    {
        SetupComponents();
        GenerateTerrain();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (autoUpdate && !Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    SetupComponents();
                    GenerateTerrain();
                }
            };
        }
#endif
    }

    void SetupComponents()
    {
        if (this == null || gameObject == null) return;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (terrainMaterial != null && meshRenderer != null)
            meshRenderer.material = terrainMaterial;
    }

    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        if (highwayCollider == null)
        {
            Debug.LogWarning("Highway Collider reference not set!");
            return;
        }

        if (highwayCollider.sharedMesh == null)
        {
            Debug.LogWarning("Highway Collider has no mesh!");
            return;
        }

        if (meshFilter == null) SetupComponents();

        CalculateHighwayBounds();
        GenerateTerrainMesh();

        if (generateCollider && meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = terrainMesh;
        }
    }

    [ContextMenu("Randomize Noise")]
    public void RandomizeNoise()
    {
        noiseOffset = new Vector2(Random.Range(-1000f, 1000f), Random.Range(-1000f, 1000f));
        GenerateTerrain();
    }

    void CalculateHighwayBounds()
    {
        // Get bounds from highway collider mesh in world space
        Mesh highwayMesh = highwayCollider.sharedMesh;
        Transform highwayTransform = highwayCollider.transform;

        Vector3 min = Vector3.one * float.MaxValue;
        Vector3 max = Vector3.one * float.MinValue;

        // Sample points along highway for fast distance checks
        Vector3[] meshVertices = highwayMesh.vertices;
        int vertexCount = meshVertices.Length;
        int sampleStep = Mathf.Max(1, vertexCount / HIGHWAY_SAMPLES);

        highwaySamplePoints = new Vector3[Mathf.Min(HIGHWAY_SAMPLES, vertexCount)];
        int sampleIndex = 0;

        // Transform mesh vertices to world space to get accurate bounds
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 worldVertex = highwayTransform.TransformPoint(meshVertices[i]);

            min.x = Mathf.Min(min.x, worldVertex.x);
            min.y = Mathf.Min(min.y, worldVertex.y);
            min.z = Mathf.Min(min.z, worldVertex.z);

            max.x = Mathf.Max(max.x, worldVertex.x);
            max.y = Mathf.Max(max.y, worldVertex.y);
            max.z = Mathf.Max(max.z, worldVertex.z);

            // Sample points for distance checks
            if (i % sampleStep == 0 && sampleIndex < highwaySamplePoints.Length)
            {
                highwaySamplePoints[sampleIndex++] = worldVertex;
            }
        }

        // Create bounds and add padding
        Vector3 size = max - min;
        terrainCenter = (min + max) / 2f;
        terrainCenter.y = 0; // Keep terrain centered at Y=0

        terrainWidth = size.x + (terrainPadding * 2f);
        terrainLength = size.z + (terrainPadding * 2f);

        highwayBounds = new Bounds(terrainCenter, new Vector3(terrainWidth, size.y, terrainLength));

        Debug.Log($"Highway bounds calculated: {terrainWidth:F1}x{terrainLength:F1}, {highwaySamplePoints.Length} sample points");
    }

    void GenerateTerrainMesh()
    {
        // Clamp grid resolution for performance
        float actualResolution = Mathf.Max(gridResolution, minGridResolution);

        // Calculate grid dimensions based on calculated bounds
        int xVerts = Mathf.CeilToInt(terrainWidth / actualResolution) + 1;
        int zVerts = Mathf.CeilToInt(terrainLength / actualResolution) + 1;

        // Safety check - limit vertex count
        int maxVerts = 10000;
        if (xVerts * zVerts > maxVerts)
        {
            Debug.LogWarning($"Terrain would have {xVerts * zVerts} vertices (max: {maxVerts}). Increase grid resolution!");
            float scale = Mathf.Sqrt((xVerts * zVerts) / (float)maxVerts);
            actualResolution *= scale;
            xVerts = Mathf.CeilToInt(terrainWidth / actualResolution) + 1;
            zVerts = Mathf.CeilToInt(terrainLength / actualResolution) + 1;
        }

        Debug.Log($"Generating terrain: {xVerts}x{zVerts} = {xVerts * zVerts} vertices");

        Vector3[] vertices = new Vector3[xVerts * zVerts];
        Vector2[] uvs = new Vector2[xVerts * zVerts];
        int[] triangles = new int[(xVerts - 1) * (zVerts - 1) * 6];

        // Generate vertices
        for (int z = 0; z < zVerts; z++)
        {
            for (int x = 0; x < xVerts; x++)
            {
                int index = z * xVerts + x;

                // World position of this vertex
                float worldX = (x * actualResolution) - (terrainWidth / 2f) + terrainCenter.x;
                float worldZ = (z * actualResolution) - (terrainLength / 2f) + terrainCenter.z;
                Vector3 worldPos = new Vector3(worldX, 0, worldZ);

                // Calculate height using multi-octave Perlin noise
                float height = CalculateHeight(worldX, worldZ);

                // Find distance to nearest highway point
                float distanceToRoad = GetDistanceToHighway(worldPos);

                // Flatten terrain near the road
                float flattenFactor = CalculateFlattenFactor(distanceToRoad);
                float roadHeight = GetRoadHeightAt(worldPos) + roadHeightOffset;

                // Blend between terrain height and road height
                height = Mathf.Lerp(roadHeight, height, flattenFactor);

                // Apply edge falloff
                float edgeFactor = CalculateEdgeFalloff(x, z, xVerts, zVerts);
                height *= edgeFactor;

                // Local position relative to terrain GameObject
                vertices[index] = new Vector3(
                    worldX - terrainCenter.x,
                    height + baseTerrainHeight,
                    worldZ - terrainCenter.z
                );

                // UVs for texture mapping
                uvs[index] = new Vector2((float)x / xVerts, (float)z / zVerts);
            }
        }

        // Generate triangles
        int triIndex = 0;
        for (int z = 0; z < zVerts - 1; z++)
        {
            for (int x = 0; x < xVerts - 1; x++)
            {
                int topLeft = z * xVerts + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * xVerts + x;
                int bottomRight = bottomLeft + 1;

                // First triangle
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topRight;

                // Second triangle
                triangles[triIndex++] = topRight;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = bottomRight;
            }
        }

        // Create or update mesh
        if (terrainMesh == null)
        {
            terrainMesh = new Mesh();
            terrainMesh.name = "Highway Terrain";
        }
        else
        {
            terrainMesh.Clear();
        }

        terrainMesh.vertices = vertices;
        terrainMesh.uv = uvs;
        terrainMesh.triangles = triangles;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();

        meshFilter.mesh = terrainMesh;
    }

    // Calculate terrain height using multi-octave Perlin noise
    float CalculateHeight(float worldX, float worldZ)
    {
        float height = 0f;
        float amplitude = hillHeight;
        float frequency = hillFrequency;

        for (int i = 0; i < noiseOctaves; i++)
        {
            float sampleX = (worldX + noiseOffset.x) * frequency;
            float sampleZ = (worldZ + noiseOffset.y) * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
            height += perlinValue * amplitude;

            amplitude *= noisePersistence;
            frequency *= 2f;
        }

        return height;
    }

    // Get distance from a point to the nearest highway surface (fast approximation)
    float GetDistanceToHighway(Vector3 worldPos)
    {
        if (highwaySamplePoints == null || highwaySamplePoints.Length == 0)
            return float.MaxValue;

        // Find nearest sample point (much faster than ClosestPoint on mesh)
        float minDist = float.MaxValue;

        foreach (Vector3 samplePoint in highwaySamplePoints)
        {
            float dist = Vector3.Distance(worldPos, samplePoint);
            if (dist < minDist)
                minDist = dist;
        }

        return minDist;
    }

    // Get the height of the road at a given position (fast approximation)
    float GetRoadHeightAt(Vector3 worldPos)
    {
        if (highwaySamplePoints == null || highwaySamplePoints.Length == 0)
            return 0f;

        // Find nearest sample point and use its height
        float minDist = float.MaxValue;
        float nearestHeight = 0f;

        foreach (Vector3 samplePoint in highwaySamplePoints)
        {
            float dist = Vector3.Distance(worldPos, samplePoint);
            if (dist < minDist)
            {
                minDist = dist;
                nearestHeight = samplePoint.y;
            }
        }

        return nearestHeight;
    }

    // Calculate how much to flatten terrain based on distance to road
    float CalculateFlattenFactor(float distance)
    {
        if (distance < flattenDistance)
        {
            // Use smooth curve for natural transition
            float normalized = distance / flattenDistance;
            return Mathf.Pow(normalized, flattenFalloff);
        }
        return 1f;
    }

    // Calculate edge falloff to make terrain drop at borders
    float CalculateEdgeFalloff(int x, int z, int xVerts, int zVerts)
    {
        if (edgeFalloff <= 0f) return 1f;

        float xFactor = Mathf.Min(x, xVerts - 1 - x) / (xVerts * edgeFalloff);
        float zFactor = Mathf.Min(z, zVerts - 1 - z) / (zVerts * edgeFalloff);

        xFactor = Mathf.Clamp01(xFactor);
        zFactor = Mathf.Clamp01(zFactor);

        return Mathf.Min(xFactor, zFactor);
    }

    void OnDrawGizmosSelected()
    {
        if (highwayCollider == null) return;

        // Draw calculated bounds
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(highwayBounds.center, highwayBounds.size);

        // Visualize flatten distance around highway
        Gizmos.color = new Color(0, 1, 0, 0.3f);

        // Sample points along the highway bounds
        int samples = 20;
        for (int i = 0; i < samples; i++)
        {
            for (int j = 0; j < samples; j++)
            {
                float xLerp = i / (float)(samples - 1);
                float zLerp = j / (float)(samples - 1);

                Vector3 samplePoint = new Vector3(
                    Mathf.Lerp(highwayBounds.min.x, highwayBounds.max.x, xLerp),
                    highwayBounds.center.y,
                    Mathf.Lerp(highwayBounds.min.z, highwayBounds.max.z, zLerp)
                );

                float distance = GetDistanceToHighway(samplePoint);

                if (distance < flattenDistance)
                {
                    float alpha = 1f - (distance / flattenDistance);
                    Gizmos.color = new Color(0, 1, 0, alpha * 0.5f);
                    Gizmos.DrawSphere(samplePoint, 0.5f);
                }
            }
        }
    }
}