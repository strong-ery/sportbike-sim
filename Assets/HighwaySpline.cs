using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// HIGHWAY SPLINE - Catmull-Rom spline with thick road mesh generation
// FIXED: Loop connection twist issue
// ============================================================================
public class HighwaySpline : MonoBehaviour
{
    [Header("Control Points")]
    public List<Transform> controlPoints = new List<Transform>();

    [Header("Road Settings")]
    [Range(2, 8)] public int laneCount = 4;
    public float laneWidth = 3.5f;
    public float shoulderWidth = 2f;
    [Range(0.5f, 5f)] public float resolution = 1f;
    public float roadThickness = 0.5f;
    public bool loopSpline = false;

    [Header("Lane Line Settings")]
    public float lineWidth = 0.15f;
    public float lineHeight = 0.02f;
    public float dashedLineLength = 3f;
    public float dashedLineGap = 6f;
    public bool solidEdgeLines = true;
    public bool dashedCenterLines = true;

    [Header("Lamp Posts")]
    public bool generateLamps = false;
    public GameObject lampPrefab;
    public float lampSpacing = 20f;

    [Header("Materials")]
    public Material roadMaterial;
    public Material linesMaterial;

    [Header("Gizmo Settings")]
    public bool showSpline = true;
    public bool showLanes = true;
    public Color splineColor = Color.yellow;

    private List<Vector3> splinePoints = new List<Vector3>();
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private GameObject linesObject;
    private GameObject lampsObject;

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && controlPoints.Count >= 2)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    SetupMeshComponents();
                    GenerateHighway();
                }
            };
        }
#endif
    }

    void SetupMeshComponents()
    {
        if (this == null || gameObject == null) return;

        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (roadMaterial != null && meshRenderer != null)
            meshRenderer.material = roadMaterial;

        if (linesObject == null)
        {
            linesObject = transform.Find("LaneLines")?.gameObject;
            if (linesObject == null)
            {
                linesObject = new GameObject("LaneLines");
                linesObject.transform.SetParent(transform);
                linesObject.transform.localPosition = Vector3.zero;
                linesObject.transform.localRotation = Quaternion.identity;
            }
        }

        if (lampsObject == null)
        {
            lampsObject = transform.Find("Lamps")?.gameObject;
            if (lampsObject == null)
            {
                lampsObject = new GameObject("Lamps");
                lampsObject.transform.SetParent(transform);
                lampsObject.transform.localPosition = Vector3.zero;
                lampsObject.transform.localRotation = Quaternion.identity;
            }
        }
    }

    [ContextMenu("Generate Highway")]
    public void GenerateHighway()
    {
        if (controlPoints.Count < 2)
        {
            Debug.LogWarning("Need at least 2 control points!");
            return;
        }

        controlPoints.RemoveAll(cp => cp == null);
        if (controlPoints.Count < 2) return;

        GenerateSplinePoints();
        GenerateRoadMesh();
        GenerateLaneLines();
        GenerateLamps();
    }

    void GenerateSplinePoints()
    {
        splinePoints.Clear();

        if (loopSpline && controlPoints.Count < 3)
        {
            Debug.LogWarning("Need at least 3 control points for a loop!");
            return;
        }

        int pointCount = loopSpline ? controlPoints.Count : controlPoints.Count - 1;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 p0, p1, p2, p3;

            if (loopSpline)
            {
                int prevIndex = (i - 1 + controlPoints.Count) % controlPoints.Count;
                int currIndex = i % controlPoints.Count;
                int nextIndex = (i + 1) % controlPoints.Count;
                int nextNextIndex = (i + 2) % controlPoints.Count;

                p0 = controlPoints[prevIndex].position;
                p1 = controlPoints[currIndex].position;
                p2 = controlPoints[nextIndex].position;
                p3 = controlPoints[nextNextIndex].position;
            }
            else
            {
                p0 = i > 0 ? controlPoints[i - 1].position : controlPoints[i].position;
                p1 = controlPoints[i].position;
                p2 = controlPoints[i + 1].position;
                p3 = i < controlPoints.Count - 2 ? controlPoints[i + 2].position : controlPoints[i + 1].position;
            }

            float segmentLength = Vector3.Distance(p1, p2);
            int segments = Mathf.Max(2, Mathf.CeilToInt(segmentLength / resolution));

            bool isLastSegment = (i == pointCount - 1);
            int endJ = (isLastSegment && !loopSpline) ? segments : segments - 1;

            for (int j = 0; j <= endJ; j++)
            {
                float t = j / (float)segments;
                Vector3 point = CatmullRom(p0, p1, p2, p3, t);
                splinePoints.Add(point);
            }
        }
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // FIXED: Proper loop connection with consistent orientation
    void GenerateRoadMesh()
    {
        if (splinePoints.Count < 2 || meshFilter == null) return;

        float totalWidth = (laneCount * laneWidth) + (2 * shoulderWidth);
        float halfWidth = totalWidth / 2f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Pre-calculate consistent right vectors for all points
        List<Vector3> rightVectors = new List<Vector3>();

        if (loopSpline)
        {
            // For loops, use parallel transport to maintain consistent orientation
            Vector3 initialRight = Vector3.zero;

            // Find initial right vector
            Vector3 firstForward = (splinePoints[1] - splinePoints[0]).normalized;
            initialRight = Vector3.Cross(Vector3.up, firstForward).normalized;

            rightVectors.Add(initialRight);

            // Propagate right vector using parallel transport
            for (int i = 1; i < splinePoints.Count; i++)
            {
                Vector3 prevForward = (splinePoints[i] - splinePoints[i - 1]).normalized;
                Vector3 currForward = (splinePoints[(i + 1) % splinePoints.Count] - splinePoints[i]).normalized;

                // Rotate previous right vector to align with new forward
                Vector3 axis = Vector3.Cross(prevForward, currForward);
                float angle = Vector3.Angle(prevForward, currForward);

                Vector3 newRight = rightVectors[i - 1];
                if (axis.magnitude > 0.001f)
                {
                    newRight = Quaternion.AngleAxis(angle, axis.normalized) * newRight;
                }

                // Ensure right vector is perpendicular to current forward
                newRight = Vector3.Cross(Vector3.up, currForward).normalized;

                rightVectors.Add(newRight);
            }

            // Smooth the transition back to start by blending
            Vector3 lastRight = rightVectors[rightVectors.Count - 1];
            Vector3 firstRightCheck = Vector3.Cross(Vector3.up,
                (splinePoints[0] - splinePoints[splinePoints.Count - 1]).normalized).normalized;

            // If there's a big difference, gradually blend the last few points
            if (Vector3.Dot(lastRight, firstRightCheck) < 0.9f)
            {
                int blendPoints = Mathf.Min(5, splinePoints.Count / 4);
                for (int i = 0; i < blendPoints; i++)
                {
                    int idx = splinePoints.Count - blendPoints + i;
                    float t = (float)i / blendPoints;
                    rightVectors[idx] = Vector3.Slerp(rightVectors[idx], initialRight, t).normalized;
                }
            }
        }
        else
        {
            // For non-looped, simple calculation
            for (int i = 0; i < splinePoints.Count; i++)
            {
                Vector3 forward = GetForwardAtIndex(i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                rightVectors.Add(right);
            }
        }

        float uvDistance = 0f;

        // Generate vertices using consistent right vectors
        for (int i = 0; i < splinePoints.Count; i++)
        {
            Vector3 center = splinePoints[i];
            Vector3 right = rightVectors[i];

            Vector3 leftTop = center - right * halfWidth;
            Vector3 rightTop = center + right * halfWidth;
            Vector3 leftBottom = leftTop - Vector3.up * roadThickness;
            Vector3 rightBottom = rightTop - Vector3.up * roadThickness;

            vertices.Add(leftTop);
            vertices.Add(rightTop);
            vertices.Add(leftBottom);
            vertices.Add(rightBottom);

            if (i > 0)
            {
                uvDistance += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);
            }

            float uvY = uvDistance / (laneWidth * 2f);
            uvs.Add(new Vector2(0, uvY));
            uvs.Add(new Vector2(1, uvY));
            uvs.Add(new Vector2(0, uvY));
            uvs.Add(new Vector2(1, uvY));
        }

        // Create triangles
        int maxIndex = loopSpline ? splinePoints.Count : splinePoints.Count - 1;

        for (int i = 0; i < maxIndex; i++)
        {
            int baseIndex = i * 4;
            int nextIndex = ((i + 1) % splinePoints.Count) * 4;

            // Top surface
            triangles.Add(baseIndex);
            triangles.Add(nextIndex);
            triangles.Add(baseIndex + 1);

            triangles.Add(baseIndex + 1);
            triangles.Add(nextIndex);
            triangles.Add(nextIndex + 1);

            // Bottom surface
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(nextIndex + 2);

            triangles.Add(baseIndex + 3);
            triangles.Add(nextIndex + 3);
            triangles.Add(nextIndex + 2);

            // Left side wall
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(nextIndex);

            triangles.Add(baseIndex + 2);
            triangles.Add(nextIndex + 2);
            triangles.Add(nextIndex);

            // Right side wall
            triangles.Add(baseIndex + 1);
            triangles.Add(nextIndex + 1);
            triangles.Add(baseIndex + 3);

            triangles.Add(baseIndex + 3);
            triangles.Add(nextIndex + 1);
            triangles.Add(nextIndex + 3);
        }

        if (!loopSpline)
        {
            AddEndCaps(vertices, triangles, uvs);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Highway Mesh";
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }

    void AddEndCaps(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        if (splinePoints.Count < 2 || loopSpline) return;

        int startCapBase = vertices.Count;
        vertices.Add(vertices[0]);
        vertices.Add(vertices[1]);
        vertices.Add(vertices[2]);
        vertices.Add(vertices[3]);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));

        triangles.Add(startCapBase);
        triangles.Add(startCapBase + 2);
        triangles.Add(startCapBase + 1);

        triangles.Add(startCapBase + 1);
        triangles.Add(startCapBase + 2);
        triangles.Add(startCapBase + 3);

        int lastIndex = (splinePoints.Count - 1) * 4;
        int endCapBase = vertices.Count;

        vertices.Add(vertices[lastIndex]);
        vertices.Add(vertices[lastIndex + 1]);
        vertices.Add(vertices[lastIndex + 2]);
        vertices.Add(vertices[lastIndex + 3]);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));

        triangles.Add(endCapBase);
        triangles.Add(endCapBase + 1);
        triangles.Add(endCapBase + 2);

        triangles.Add(endCapBase + 1);
        triangles.Add(endCapBase + 3);
        triangles.Add(endCapBase + 2);
    }

    void GenerateLaneLines()
    {
        if (splinePoints.Count < 2 || linesObject == null || linesMaterial == null) return;

        foreach (Transform child in linesObject.transform)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        float totalWidth = (laneCount * laneWidth) + (2 * shoulderWidth);

        for (int lane = -laneCount / 2; lane <= laneCount / 2; lane++)
        {
            float offset = lane * laneWidth;
            bool isEdge = (lane == -laneCount / 2 || lane == laneCount / 2);

            bool useDashed = dashedCenterLines && !isEdge;
            bool useSolid = solidEdgeLines && isEdge;

            if (useSolid || !isEdge)
            {
                GenerateLaneLine(offset, useDashed);
            }
        }
    }

    void GenerateLaneLine(float lateralOffset, bool dashed)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float currentDashDistance = 0f;
        bool drawingDash = true;

        int maxIndex = loopSpline ? splinePoints.Count : splinePoints.Count - 1;

        for (int i = 0; i < maxIndex; i++)
        {
            Vector3 center = splinePoints[i];
            Vector3 nextCenter = splinePoints[(i + 1) % splinePoints.Count];
            Vector3 forward = GetForwardAtIndex(i);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 nextForward = GetForwardAtIndex((i + 1) % splinePoints.Count);
            Vector3 nextRight = Vector3.Cross(Vector3.up, nextForward).normalized;

            Vector3 linePos = center + right * lateralOffset + Vector3.up * lineHeight;
            Vector3 nextLinePos = nextCenter + nextRight * lateralOffset + Vector3.up * lineHeight;

            float segmentLength = Vector3.Distance(linePos, nextLinePos);

            if (dashed)
            {
                float remainingLength = segmentLength;
                float segmentStart = 0f;

                while (remainingLength > 0)
                {
                    float dashLength = drawingDash ? dashedLineLength : dashedLineGap;
                    float lengthToDraw = Mathf.Min(dashLength - currentDashDistance, remainingLength);

                    if (drawingDash)
                    {
                        float tStart = segmentStart / segmentLength;
                        float tEnd = (segmentStart + lengthToDraw) / segmentLength;

                        Vector3 dashStart = Vector3.Lerp(linePos, nextLinePos, tStart);
                        Vector3 dashEnd = Vector3.Lerp(linePos, nextLinePos, tEnd);

                        AddLineSegment(vertices, triangles, uvs, dashStart, dashEnd, forward);
                    }

                    currentDashDistance += lengthToDraw;
                    segmentStart += lengthToDraw;
                    remainingLength -= lengthToDraw;

                    if (currentDashDistance >= (drawingDash ? dashedLineLength : dashedLineGap))
                    {
                        drawingDash = !drawingDash;
                        currentDashDistance = 0f;
                    }
                }
            }
            else
            {
                AddLineSegment(vertices, triangles, uvs, linePos, nextLinePos, forward);
            }
        }

        if (vertices.Count > 0)
        {
            GameObject lineObj = new GameObject(dashed ? "DashedLine" : "SolidLine");
            lineObj.transform.SetParent(linesObject.transform);
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localRotation = Quaternion.identity;

            MeshFilter mf = lineObj.AddComponent<MeshFilter>();
            MeshRenderer mr = lineObj.AddComponent<MeshRenderer>();

            Mesh lineMesh = new Mesh();
            lineMesh.name = dashed ? "Dashed Lane Line" : "Solid Lane Line";
            lineMesh.vertices = vertices.ToArray();
            lineMesh.triangles = triangles.ToArray();
            lineMesh.uv = uvs.ToArray();
            lineMesh.RecalculateNormals();
            lineMesh.RecalculateBounds();

            mf.mesh = lineMesh;
            mr.material = linesMaterial;
        }
    }

    void AddLineSegment(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
                        Vector3 start, Vector3 end, Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float halfWidth = lineWidth / 2f;

        int baseIndex = vertices.Count;

        vertices.Add(start - right * halfWidth);
        vertices.Add(start + right * halfWidth);
        vertices.Add(end - right * halfWidth);
        vertices.Add(end + right * halfWidth);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);

        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    void GenerateLamps()
    {
        if (!generateLamps || lampPrefab == null || lampsObject == null || splinePoints.Count < 2)
        {
            if (lampsObject != null)
            {
                foreach (Transform child in lampsObject.transform)
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
            return;
        }

        foreach (Transform child in lampsObject.transform)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        float totalLength = 0f;
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            totalLength += Vector3.Distance(splinePoints[i], splinePoints[i + 1]);
        }

        float currentDistance = 0f;
        int lampIndex = 0;

        while (currentDistance <= totalLength)
        {
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;

            if (GetPositionAndRotationAtDistance(currentDistance, out position, out rotation))
            {
#if UNITY_EDITOR
                GameObject lamp;
                if (Application.isPlaying)
                {
                    lamp = Instantiate(lampPrefab, position, rotation, lampsObject.transform);
                }
                else
                {
                    lamp = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(lampPrefab, lampsObject.transform);
                    lamp.transform.position = position;
                    lamp.transform.rotation = rotation;
                }
#else
                GameObject lamp = Instantiate(lampPrefab, position, rotation, lampsObject.transform);
#endif
                lamp.name = $"Lamp_{lampIndex}";
                lampIndex++;
            }

            currentDistance += lampSpacing;
        }
    }

    bool GetPositionAndRotationAtDistance(float distance, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (splinePoints.Count < 2)
            return false;

        float accumulatedDistance = 0f;

        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(splinePoints[i], splinePoints[i + 1]);

            if (accumulatedDistance + segmentLength >= distance)
            {
                float t = (distance - accumulatedDistance) / segmentLength;
                position = Vector3.Lerp(splinePoints[i], splinePoints[i + 1], t);

                Vector3 forward = (splinePoints[i + 1] - splinePoints[i]).normalized;
                rotation = Quaternion.LookRotation(forward, Vector3.up);

                return true;
            }

            accumulatedDistance += segmentLength;
        }

        if (distance >= accumulatedDistance && splinePoints.Count > 0)
        {
            position = splinePoints[splinePoints.Count - 1];
            Vector3 forward = GetForwardAtIndex(splinePoints.Count - 1);
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            return true;
        }

        return false;
    }

    Vector3 GetForwardAtIndex(int index)
    {
        if (loopSpline)
        {
            int nextIndex = (index + 1) % splinePoints.Count;
            return (splinePoints[nextIndex] - splinePoints[index]).normalized;
        }
        else if (index < splinePoints.Count - 1)
        {
            return (splinePoints[index + 1] - splinePoints[index]).normalized;
        }
        else if (index > 0)
        {
            return (splinePoints[index] - splinePoints[index - 1]).normalized;
        }
        return Vector3.forward;
    }

    public Vector3 GetClosestPoint(Vector3 worldPos)
    {
        if (splinePoints.Count == 0) return worldPos;

        Vector3 closest = splinePoints[0];
        float minDist = float.MaxValue;

        foreach (Vector3 point in splinePoints)
        {
            float dist = Vector3.Distance(worldPos, point);
            if (dist < minDist)
            {
                minDist = dist;
                closest = point;
            }
        }

        return closest;
    }

    public Vector3 GetPointAtDistance(float normalizedDistance)
    {
        if (splinePoints.Count == 0) return Vector3.zero;

        normalizedDistance = Mathf.Clamp01(normalizedDistance);
        int index = Mathf.FloorToInt(normalizedDistance * (splinePoints.Count - 1));

        if (index >= splinePoints.Count - 1)
            return splinePoints[splinePoints.Count - 1];

        float t = (normalizedDistance * (splinePoints.Count - 1)) - index;
        return Vector3.Lerp(splinePoints[index], splinePoints[index + 1], t);
    }

    void OnDrawGizmos()
    {
        if (!showSpline || splinePoints.Count < 2) return;

        Gizmos.color = splineColor;
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            Gizmos.DrawLine(splinePoints[i], splinePoints[i + 1]);
        }

        if (loopSpline && splinePoints.Count > 0)
        {
            Gizmos.DrawLine(splinePoints[splinePoints.Count - 1], splinePoints[0]);
        }

        if (showLanes)
        {
            float totalWidth = (laneCount * laneWidth) + (2 * shoulderWidth);

            Gizmos.color = Color.white;

            int maxIndex = loopSpline ? splinePoints.Count : splinePoints.Count - 1;

            for (int i = 0; i < maxIndex; i++)
            {
                Vector3 center = splinePoints[i];
                Vector3 forward = GetForwardAtIndex(i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                for (int lane = -laneCount / 2; lane <= laneCount / 2; lane++)
                {
                    float offset = lane * laneWidth;
                    Vector3 lanePos = center + right * offset;

                    int nextIdx = (i + 1) % splinePoints.Count;
                    Vector3 nextLanePos = splinePoints[nextIdx] +
                        Vector3.Cross(Vector3.up, GetForwardAtIndex(nextIdx)).normalized * offset;

                    Gizmos.DrawLine(lanePos, nextLanePos);
                }
            }
        }

        Gizmos.color = Color.red;
        foreach (Transform cp in controlPoints)
        {
            if (cp != null)
                Gizmos.DrawWireSphere(cp.position, 2f);
        }
    }
}