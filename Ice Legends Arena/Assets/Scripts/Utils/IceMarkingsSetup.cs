using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates ice hockey rink markings including center circle, face-off circles, and blue lines.
/// Based on international ice hockey rink standards.
/// </summary>
public class IceMarkingsSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Ice Rink Markings")]
    public static void CreateIceMarkings()
    {
        // Rink dimensions
        float rinkLength = 61f;  // 61m
        float rinkWidth = 30f;   // 30m

        // Marking dimensions (international standard)
        float centerCircleDiameter = 9f;       // Center ice circle
        float centerDotDiameter = 0.6f;        // Center faceoff dot
        float faceoffCircleDiameter = 9f;      // Faceoff circles
        float faceoffDotDiameter = 0.6f;       // Faceoff dots
        float blueLineWidth = 0.3f;            // Blue line thickness

        // Positions
        float blueLineDistance = 8.83f;        // Distance from center to blue line
        float goalLineDistance = 4f;           // Distance from boards to goal line
        float faceoffFromGoalLine = 6.7f;      // Distance from goal line to end zone faceoff

        // Create or find IceMarkings container
        GameObject markings = GameObject.Find("IceMarkings");
        if (markings == null)
        {
            markings = new GameObject("IceMarkings");
        }

        // Clear existing markings
        while (markings.transform.childCount > 0)
        {
            DestroyImmediate(markings.transform.GetChild(0).gameObject);
        }

        // Create center ice markings
        CreateCenterCircle(markings.transform, centerCircleDiameter);
        CreateCenterDot(markings.transform, centerDotDiameter);
        CreateCenterLine(markings.transform, rinkWidth, blueLineWidth);

        // Create blue lines (zone dividers)
        CreateBlueLine(markings.transform, "BlueLineWest", -blueLineDistance, rinkWidth, blueLineWidth);
        CreateBlueLine(markings.transform, "BlueLineEast", blueLineDistance, rinkWidth, blueLineWidth);

        // Create end zone faceoff circles (4 total)
        float faceoffX = (rinkLength / 2f) - goalLineDistance - faceoffFromGoalLine;
        float faceoffY = 7f; // Offset from center

        CreateFaceoffCircle(markings.transform, "WestFaceoffTop", -faceoffX, faceoffY, faceoffCircleDiameter, faceoffDotDiameter);
        CreateFaceoffCircle(markings.transform, "WestFaceoffBottom", -faceoffX, -faceoffY, faceoffCircleDiameter, faceoffDotDiameter);
        CreateFaceoffCircle(markings.transform, "EastFaceoffTop", faceoffX, faceoffY, faceoffCircleDiameter, faceoffDotDiameter);
        CreateFaceoffCircle(markings.transform, "EastFaceoffBottom", faceoffX, -faceoffY, faceoffCircleDiameter, faceoffDotDiameter);

        // Create neutral zone faceoff dots (4 total)
        float neutralX = blueLineDistance + 1.5f; // 1.5m from blue line
        float neutralY = 7f;

        CreateFaceoffDot(markings.transform, "NeutralWestTop", -neutralX, neutralY, faceoffDotDiameter);
        CreateFaceoffDot(markings.transform, "NeutralWestBottom", -neutralX, -neutralY, faceoffDotDiameter);
        CreateFaceoffDot(markings.transform, "NeutralEastTop", neutralX, neutralY, faceoffDotDiameter);
        CreateFaceoffDot(markings.transform, "NeutralEastBottom", neutralX, -neutralY, faceoffDotDiameter);

        Debug.Log("Ice rink markings created successfully!");
    }

    private static void CreateCenterCircle(Transform parent, float diameter)
    {
        GameObject circle = new GameObject("CenterCircle");
        circle.transform.SetParent(parent);
        circle.transform.localPosition = Vector3.zero;

        LineRenderer line = circle.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(0.2f, 0.4f, 0.8f, 1f); // Blue
        line.endColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        line.startWidth = 0.15f;
        line.endWidth = 0.15f;
        line.sortingOrder = 2;
        line.numCapVertices = 5;

        DrawCircle(line, diameter / 2f, 64);
    }

    private static void CreateCenterDot(Transform parent, float diameter)
    {
        GameObject dot = new GameObject("CenterDot");
        dot.transform.SetParent(parent);
        dot.transform.localPosition = Vector3.zero;

        SpriteRenderer sprite = dot.AddComponent<SpriteRenderer>();
        sprite.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sprite.color = new Color(0.2f, 0.4f, 0.8f, 1f); // Blue
        sprite.sortingOrder = 3;
        dot.transform.localScale = new Vector3(diameter * 5, diameter * 5, 1);
    }

    private static void CreateCenterLine(Transform parent, float width, float lineWidth)
    {
        GameObject line = new GameObject("CenterRedLine");
        line.transform.SetParent(parent);
        line.transform.localPosition = Vector3.zero;

        LineRenderer lr = line.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.red;
        lr.endColor = Color.red;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.sortingOrder = 2;
        lr.numCapVertices = 5;

        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(0, -width / 2f, 0));
        lr.SetPosition(1, new Vector3(0, width / 2f, 0));
    }

    private static void CreateBlueLine(Transform parent, string name, float xPosition, float width, float lineWidth)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent);
        line.transform.localPosition = new Vector3(xPosition, 0, 0);

        LineRenderer lr = line.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0.2f, 0.4f, 0.8f, 1f); // Blue
        lr.endColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.sortingOrder = 2;
        lr.numCapVertices = 5;

        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(0, -width / 2f, 0));
        lr.SetPosition(1, new Vector3(0, width / 2f, 0));
    }

    private static void CreateFaceoffCircle(Transform parent, string name, float x, float y, float circleDiameter, float dotDiameter)
    {
        GameObject faceoff = new GameObject(name);
        faceoff.transform.SetParent(parent);
        faceoff.transform.localPosition = new Vector3(x, y, 0);

        // Create circle
        GameObject circle = new GameObject("Circle");
        circle.transform.SetParent(faceoff.transform);
        circle.transform.localPosition = Vector3.zero;

        LineRenderer line = circle.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.red;
        line.endColor = Color.red;
        line.startWidth = 0.1f;
        line.endWidth = 0.1f;
        line.sortingOrder = 2;
        line.numCapVertices = 5;

        DrawCircle(line, circleDiameter / 2f, 64);

        // Create center dot
        GameObject dot = new GameObject("Dot");
        dot.transform.SetParent(faceoff.transform);
        dot.transform.localPosition = Vector3.zero;

        SpriteRenderer sprite = dot.AddComponent<SpriteRenderer>();
        sprite.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sprite.color = Color.red;
        sprite.sortingOrder = 3;
        dot.transform.localScale = new Vector3(dotDiameter * 5, dotDiameter * 5, 1);
    }

    private static void CreateFaceoffDot(Transform parent, string name, float x, float y, float diameter)
    {
        GameObject dot = new GameObject(name);
        dot.transform.SetParent(parent);
        dot.transform.localPosition = new Vector3(x, y, 0);

        SpriteRenderer sprite = dot.AddComponent<SpriteRenderer>();
        sprite.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sprite.color = Color.red;
        sprite.sortingOrder = 3;
        dot.transform.localScale = new Vector3(diameter * 5, diameter * 5, 1);
    }

    private static void DrawCircle(LineRenderer line, float radius, int segments)
    {
        line.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * 360f * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            line.SetPosition(i, new Vector3(x, y, 0));
        }
    }
#endif
}
