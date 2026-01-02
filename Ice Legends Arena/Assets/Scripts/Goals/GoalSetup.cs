using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sets up hockey goals at both ends of the rink with nets and scoring triggers.
/// Creates goals positioned at the north and south ends with proper dimensions.
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
        float goalWidth = 5.5f;     // Scaled up (was 1.83m)
        float goalHeight = 3.66f;   // Scaled up (was 1.22m) - not used in 2D
        float goalDepth = 3.5f;     // Scaled up (was 1.12m)

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

        // Create East and West goals (goals are at left and right ends)
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
        BoxCollider2D triggerCollider = trigger.AddComponent<BoxCollider2D>();
        triggerCollider.size = new Vector2(depth * 0.8f, width); // Slightly smaller depth
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
        BoxCollider2D netCollider = netZone.AddComponent<BoxCollider2D>();
        netCollider.size = new Vector2(depth, width);
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
        float halfWidth = width / 2f;   // Y dimension (top/bottom)
        float halfDepth = depth / 2f;   // X dimension (front/back)

        // For East/West goals in top-down 2D:
        // X-axis: goal depth (front to back)
        // Y-axis: goal width (left side to right side of opening)

        // Shift the entire goal frame back by half the depth to align with visual
        float frameOffset = -halfDepth * 0.6f; // Shift toward back of goal

        // LEFT SIDE POST (bottom post) - runs along the depth of the goal
        CreatePost(frame.transform, "LeftSidePost", new Vector2(frameOffset, -halfWidth - postWidth/2), new Vector2(depth + postWidth, postWidth), false, false);

        // RIGHT SIDE POST (top post) - runs along the depth of the goal
        CreatePost(frame.transform, "RightSidePost", new Vector2(frameOffset, halfWidth + postWidth/2), new Vector2(depth + postWidth, postWidth), false, false);

        // BACK WALL - runs across the width at the back of the goal (absorbs pucks, no bounce)
        CreatePost(frame.transform, "BackWall", new Vector2(-halfDepth + frameOffset - postWidth/2, 0), new Vector2(postWidth, width + postWidth * 2), false, true);

        // FRONT opening barrier - blocks players but allows pucks through
        GameObject frontBarrier = new GameObject("FrontBarrier_PlayersOnly");
        frontBarrier.transform.SetParent(frame.transform);
        frontBarrier.transform.localPosition = new Vector3(halfDepth + frameOffset + postWidth/2, 0, 0);

        // Add Static Rigidbody2D
        Rigidbody2D frontRb = frontBarrier.AddComponent<Rigidbody2D>();
        frontRb.bodyType = RigidbodyType2D.Static;

        // Add visual (semi-transparent yellow to distinguish from solid posts)
        SpriteRenderer frontSprite = frontBarrier.AddComponent<SpriteRenderer>();
        frontSprite.sprite = CreateSquareSprite();
        frontSprite.color = new Color(1f, 1f, 0f, 0.4f); // Semi-transparent yellow
        frontSprite.sortingOrder = 9;
        frontBarrier.transform.localScale = new Vector3(postWidth, width, 1);

        BoxCollider2D frontCollider = frontBarrier.AddComponent<BoxCollider2D>();
        frontCollider.size = Vector2.one; // Size controlled by transform scale
        frontCollider.isTrigger = true; // Make it a trigger so pucks pass through

        // Add script to block players but allow pucks
        FrontBarrierTrigger barrierScript = frontBarrier.AddComponent<FrontBarrierTrigger>();
    }

    private static void CreatePost(Transform parent, string name, Vector2 position, Vector2 size, bool isPuckPassable, bool isBackWall = false)
    {
        GameObject post = new GameObject(name);
        post.transform.SetParent(parent);
        post.transform.localPosition = position;

        Debug.Log($"Creating post '{name}' at position {position} with size {size}");

        // Add Rigidbody2D set to Static so it interacts properly with dynamic objects
        Rigidbody2D rb = post.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static; // Static = immovable but participates in physics

        // Add visual representation so we can see where posts are
        SpriteRenderer sprite = post.AddComponent<SpriteRenderer>();
        sprite.sprite = CreateSquareSprite();
        sprite.color = new Color(0.8f, 0.1f, 0.1f, 1f); // Fully opaque red for better visibility
        sprite.sortingOrder = 10;
        post.transform.localScale = new Vector3(size.x, size.y, 1);

        // Add BoxCollider2D
        BoxCollider2D collider = post.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one; // Size is 1x1 because we're scaling the transform
        collider.enabled = true; // Explicitly enable

        Debug.Log($"Post '{name}' - Collider size: {collider.size}, Transform scale: {post.transform.localScale}, World position: {post.transform.position}");

        if (!isPuckPassable)
        {
            PhysicsMaterial2D postMaterial = new PhysicsMaterial2D(isBackWall ? "BackWallMaterial" : "PostMaterial");
            if (isBackWall)
            {
                // Back wall absorbs shots - no bounce
                postMaterial.friction = 0.5f;
                postMaterial.bounciness = 0.0f; // Zero bounce - puck stops
            }
            else
            {
                // Side posts - some bounce for realistic deflections
                postMaterial.friction = 0.2f;
                postMaterial.bounciness = 0.3f;
            }
            collider.sharedMaterial = postMaterial;
        }
    }

    private static Sprite CreateSquareSprite()
    {
        // Use the built-in white square sprite
        return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
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

        // Draw goal frame outline (top-down view)
        // Rectangle showing goal opening (for East/West goals, width is along Y, depth along X)
        Vector3[] framePoints = new Vector3[5];
        float halfWidth = width / 2f;   // Y dimension
        float halfDepth = depth / 2f;   // X dimension

        // Draw as a rectangle (goal mouth) - oriented for East/West goals
        framePoints[0] = new Vector3(-halfDepth, -halfWidth, 0);   // Back-left
        framePoints[1] = new Vector3(halfDepth, -halfWidth, 0);    // Front-left
        framePoints[2] = new Vector3(halfDepth, halfWidth, 0);     // Front-right
        framePoints[3] = new Vector3(-halfDepth, halfWidth, 0);    // Back-right
        framePoints[4] = new Vector3(-halfDepth, -halfWidth, 0);   // Close the loop

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

        float halfWidth = width / 2f;   // Y dimension
        float halfDepth = depth / 2f;   // X dimension

        // Create horizontal net lines (along X-axis / depth)
        int horizontalLines = 5;
        for (int i = 1; i < horizontalLines; i++)
        {
            float t = i / (float)horizontalLines;
            float y = Mathf.Lerp(-halfWidth, halfWidth, t);

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
            lr.SetPosition(0, new Vector3(-halfDepth, y, 0));
            lr.SetPosition(1, new Vector3(halfDepth, y, 0));
        }

        // Create vertical net lines (along Y-axis / width)
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
            lr.SetPosition(0, new Vector3(x, -halfWidth, 0));
            lr.SetPosition(1, new Vector3(x, halfWidth, 0));
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

        float halfWidth = width / 2f;  // Y dimension
        // Goal line at the goal plane (4m from boards)
        // For West goal (left/player defends), line is at right side of goal (toward center)
        // For East goal (right/opponent defends), line is at left side of goal (toward center)
        float lineX = isPlayerGoal ? 0.5f : -0.5f;

        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(lineX, -halfWidth, 0));
        lr.SetPosition(1, new Vector3(lineX, halfWidth, 0));
    }
#endif
}
