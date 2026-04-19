using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sets up hockey goals at both ends of the rink with nets and scoring triggers.
/// Creates goals positioned at the east and west ends with proper dimensions.
/// Converted to 3D physics (X = rink length, Z = rink width, Y = height).
/// </summary>
public class GoalSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Setup Hockey Goals")]
    public static void SetupGoals()
    {
        // Scale up goals for gameplay (real goals are too small for this scale)
        // Real international: 1.83m x 1.22m x 1.12m
        // Scaled up 3x for better gameplay with 0.5m puck
        float goalWidth = 5.5f;     // Z-axis: opening width (was Y in 2D)
        float goalHeight = 3.66f;   // Y-axis: height above ice
        float goalDepth = 3.5f;     // X-axis: depth into the net

        // Rink dimensions (matching international 61m x 30m rink)
        float rinkLength = 61f;

        // Position goals 4m from the end boards (goal line position)
        float goalLineDistance = 4f;
        float goalXPosition = (rinkLength / 2f) - goalLineDistance;

        // Create or find Goals container
        GameObject goalsContainer = GameObject.Find("Goals");
        if (goalsContainer == null)
        {
            goalsContainer = new GameObject("Goals");
        }

        // Clear existing goals
        while (goalsContainer.transform.childCount > 0)
        {
            DestroyImmediate(goalsContainer.transform.GetChild(0).gameObject);
        }

        // Create East and West goals (goals are at left and right ends along X)
        // West goal (left) = Player defends this = isPlayerGoal = true
        // East goal (right) = Opponent defends this = isPlayerGoal = false
        CreateGoal(goalsContainer.transform, "WestGoal", new Vector3(-goalXPosition, 0, 0), goalWidth, goalHeight, goalDepth, true);
        CreateGoal(goalsContainer.transform, "EastGoal", new Vector3(goalXPosition, 0, 0), goalWidth, goalHeight, goalDepth, false);

        Debug.Log("Hockey goals created successfully!");
        Debug.Log($"Goal dimensions: {goalWidth}m (w) x {goalHeight}m (h) x {goalDepth}m (d)");
        Debug.Log($"Goals positioned {goalLineDistance}m from end boards");
    }

    private static void CreateGoal(Transform parent, string name, Vector3 position, float width, float height, float depth, bool isPlayerGoal)
    {
        GameObject goal = new GameObject(name);
        goal.transform.position = position;  // Set position BEFORE parenting to ensure correct world position
        goal.transform.SetParent(parent, true);  // worldPositionStays = true

        // Create scoring trigger zone (invisible, inside the goal)
        CreateScoringTrigger(goal.transform, width, depth, isPlayerGoal, name);

        // Create net physics zone (slows down pucks)
        CreateNetPhysicsZone(goal.transform, width, depth);

        // Create solid goal frame (posts and crossbar)
        CreateSolidGoalFrame(goal.transform, width, depth);

        // Create visual goal posts and net
        CreateGoalVisuals(goal.transform, width, height, depth, isPlayerGoal);
    }

    private static void CreateScoringTrigger(Transform parent, float width, float depth, bool isPlayerGoal, string goalName)
    {
        GameObject trigger = new GameObject("ScoringTrigger");
        trigger.transform.SetParent(parent);
        trigger.transform.localPosition = Vector3.zero;

        // Trigger zone is slightly inside the goal
        // X = depth, Z = width (opening), Y = height (generous for detection)
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.size = new Vector3(depth * 0.8f, 2f, width); // Slightly smaller depth, generous Y height
        triggerCollider.isTrigger = true;

        // Add goal trigger script to detect scoring
        GoalTrigger goalTrigger = trigger.AddComponent<GoalTrigger>();
        // Use reflection to set the private isPlayerGoal field
        goalTrigger.GetType().GetField("isPlayerGoal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(goalTrigger, isPlayerGoal);
    }

    private static void CreateNetPhysicsZone(Transform parent, float width, float depth)
    {
        GameObject netZone = new GameObject("NetPhysicsZone");
        netZone.transform.SetParent(parent);
        netZone.transform.localPosition = Vector3.zero;

        // Net zone covers the entire goal interior
        // X = depth, Z = width, Y = height (generous)
        BoxCollider netCollider = netZone.AddComponent<BoxCollider>();
        netCollider.size = new Vector3(depth, 2f, width);
        netCollider.isTrigger = true;

        // Add net physics script to slow down pucks
        netZone.AddComponent<NetPhysics>();
    }

    private static void CreateSolidGoalFrame(Transform parent, float width, float depth)
    {
        GameObject frame = new GameObject("GoalFrame_Solid");
        frame.transform.SetParent(parent);
        frame.transform.localPosition = Vector3.zero;

        float postWidth = 0.4f; // Goal post thickness (increased for visibility)
        float halfWidth = width / 2f;   // Z dimension (left/right of opening)
        float halfDepth = depth / 2f;   // X dimension (front/back)

        // For East/West goals in 3D:
        // X-axis: goal depth (front to back)
        // Z-axis: goal width (left side to right side of opening)
        // Y-axis: height (not used for post placement on ice)

        // Shift the entire goal frame back by half the depth to align with visual
        float frameOffset = -halfDepth * 0.6f; // Shift toward back of goal

        // LEFT SIDE POST - runs along the depth of the goal (negative Z side)
        CreatePost(frame.transform, "LeftSidePost",
            new Vector3(frameOffset, 0, -halfWidth - postWidth / 2),
            new Vector3(depth + postWidth, 1f, postWidth), false, false);

        // RIGHT SIDE POST - runs along the depth of the goal (positive Z side)
        CreatePost(frame.transform, "RightSidePost",
            new Vector3(frameOffset, 0, halfWidth + postWidth / 2),
            new Vector3(depth + postWidth, 1f, postWidth), false, false);

        // BACK WALL - runs across the width at the back of the goal (absorbs pucks, no bounce)
        CreatePost(frame.transform, "BackWall",
            new Vector3(-halfDepth + frameOffset - postWidth / 2, 0, 0),
            new Vector3(postWidth, 1f, width + postWidth * 2), false, true);

        // FRONT opening barrier - blocks players but allows pucks through
        GameObject frontBarrier = new GameObject("FrontBarrier_PlayersOnly");
        frontBarrier.transform.SetParent(frame.transform);
        frontBarrier.transform.localPosition = new Vector3(halfDepth + frameOffset + postWidth / 2, 0, 0);

        // Add kinematic Rigidbody (replaces RigidbodyType2D.Static)
        Rigidbody frontRb = frontBarrier.AddComponent<Rigidbody>();
        frontRb.isKinematic = true;

        // TODO: Replace SpriteRenderer with 3D mesh for front barrier visual
        // For now, use a simple cube scale for the collider
        frontBarrier.transform.localScale = new Vector3(postWidth, 1f, width);

        BoxCollider frontCollider = frontBarrier.AddComponent<BoxCollider>();
        frontCollider.size = Vector3.one; // Size controlled by transform scale
        frontCollider.isTrigger = true; // Make it a trigger so pucks pass through

        // Add script to block players but allow pucks
        frontBarrier.AddComponent<FrontBarrierTrigger>();
    }

    private static void CreatePost(Transform parent, string name, Vector3 position, Vector3 size, bool isPuckPassable, bool isBackWall = false)
    {
        GameObject post = new GameObject(name);
        post.transform.SetParent(parent);
        post.transform.localPosition = position;

        Debug.Log($"Creating post '{name}' at position {position} with size {size}");

        // Add kinematic Rigidbody (replaces RigidbodyType2D.Static)
        Rigidbody rb = post.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // TODO: Replace SpriteRenderer with 3D mesh for goal post visuals
        // For now, set transform scale for collider sizing
        post.transform.localScale = size;

        // Add BoxCollider
        BoxCollider collider = post.AddComponent<BoxCollider>();
        collider.size = Vector3.one; // Size is 1x1x1 because we're scaling the transform
        collider.enabled = true; // Explicitly enable

        Debug.Log($"Post '{name}' - Collider size: {collider.size}, Transform scale: {post.transform.localScale}, World position: {post.transform.position}");

        if (!isPuckPassable)
        {
            PhysicsMaterial postMaterial = new PhysicsMaterial(isBackWall ? "BackWallMaterial" : "PostMaterial");
            if (isBackWall)
            {
                // Back wall absorbs shots - no bounce
                postMaterial.dynamicFriction = 0.5f;
                postMaterial.staticFriction = 0.5f;
                postMaterial.bounciness = 0.0f; // Zero bounce - puck stops
                postMaterial.frictionCombine = PhysicsMaterialCombine.Average;
                postMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
            }
            else
            {
                // Side posts - some bounce for realistic deflections
                postMaterial.dynamicFriction = 0.2f;
                postMaterial.staticFriction = 0.2f;
                postMaterial.bounciness = 0.3f;
                postMaterial.frictionCombine = PhysicsMaterialCombine.Average;
                postMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            }
            collider.sharedMaterial = postMaterial;
        }
    }

    private static void CreateGoalVisuals(Transform parent, float width, float height, float depth, bool isPlayerGoal)
    {
        // Create goal frame using LineRenderer
        GameObject frame = new GameObject("GoalFrame");
        frame.transform.SetParent(parent);
        frame.transform.localPosition = Vector3.zero;

        LineRenderer lineRenderer = frame.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;  // Use local space so positions are relative to parent
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;
        lineRenderer.sortingOrder = 2;
        lineRenderer.numCapVertices = 5;

        // Draw goal frame outline (top-down view on XZ plane)
        // Rectangle showing goal opening: X = depth, Z = width
        Vector3[] framePoints = new Vector3[5];
        float halfWidth = width / 2f;   // Z dimension
        float halfDepth = depth / 2f;   // X dimension

        // Draw as a rectangle (goal mouth) - oriented for East/West goals on XZ plane
        framePoints[0] = new Vector3(-halfDepth, 0, -halfWidth);   // Back-left
        framePoints[1] = new Vector3(halfDepth, 0, -halfWidth);    // Front-left
        framePoints[2] = new Vector3(halfDepth, 0, halfWidth);     // Front-right
        framePoints[3] = new Vector3(-halfDepth, 0, halfWidth);    // Back-right
        framePoints[4] = new Vector3(-halfDepth, 0, -halfWidth);   // Close the loop

        lineRenderer.positionCount = framePoints.Length;
        lineRenderer.SetPositions(framePoints);

        // Create net visualization using multiple lines
        CreateNetLines(parent, width, depth, isPlayerGoal);

        // Create goal line (the red line at the goal mouth)
        CreateGoalLine(parent, width, isPlayerGoal);
    }

    private static void CreateNetLines(Transform parent, float width, float depth, bool isPlayerGoal)
    {
        GameObject net = new GameObject("Net");
        net.transform.SetParent(parent);
        net.transform.localPosition = Vector3.zero;

        float halfWidth = width / 2f;   // Z dimension
        float halfDepth = depth / 2f;   // X dimension

        // Create horizontal net lines (along X-axis / depth, across Z width)
        int horizontalLines = 5;
        for (int i = 1; i < horizontalLines; i++)
        {
            float t = i / (float)horizontalLines;
            float z = Mathf.Lerp(-halfWidth, halfWidth, t);

            GameObject line = new GameObject($"NetLineH{i}");
            line.transform.SetParent(net.transform);
            line.transform.localPosition = Vector3.zero;

            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 1f, 1f, 0.5f);
            lr.endColor = new Color(1f, 1f, 1f, 0.5f);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.sortingOrder = 1;

            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-halfDepth, 0, z));
            lr.SetPosition(1, new Vector3(halfDepth, 0, z));
        }

        // Create vertical net lines (along Z-axis / width, across X depth)
        int verticalLines = 4;
        for (int i = 1; i < verticalLines; i++)
        {
            float t = i / (float)verticalLines;
            float x = Mathf.Lerp(-halfDepth, halfDepth, t);

            GameObject line = new GameObject($"NetLineV{i}");
            line.transform.SetParent(net.transform);
            line.transform.localPosition = Vector3.zero;

            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 1f, 1f, 0.5f);
            lr.endColor = new Color(1f, 1f, 1f, 0.5f);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.sortingOrder = 1;

            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(x, 0, -halfWidth));
            lr.SetPosition(1, new Vector3(x, 0, halfWidth));
        }
    }

    private static void CreateGoalLine(Transform parent, float width, bool isPlayerGoal)
    {
        GameObject goalLine = new GameObject("GoalLine");
        goalLine.transform.SetParent(parent);
        goalLine.transform.localPosition = Vector3.zero;

        LineRenderer lr = goalLine.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.red;
        lr.endColor = Color.red;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.sortingOrder = 3;
        lr.numCapVertices = 5;

        float halfWidth = width / 2f;  // Z dimension
        // Goal line at the goal plane (4m from boards)
        // For West goal (left/player defends), line is at right side of goal (toward center)
        // For East goal (right/opponent defends), line is at left side of goal (toward center)
        float lineX = isPlayerGoal ? 0.5f : -0.5f;

        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(lineX, 0, -halfWidth));
        lr.SetPosition(1, new Vector3(lineX, 0, halfWidth));
    }
#endif
}
