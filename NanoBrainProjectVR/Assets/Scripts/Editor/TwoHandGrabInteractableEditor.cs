using UnityEditor;


[CustomEditor(typeof(TwoHandGrabInteractable))]
public class TwoHandGrabInteractableEditor : UnityEditor.XR.Interaction.Toolkit.Interactables.XRGrabInteractableEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // Draw the default XR Interactable UI (huge list of colliders, interactions, etc.)

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Custom Two-Hand Settings", EditorStyles.boldLabel);
        
        serializedObject.Update();

        // Draw our custom variables at the very bottom
        EditorGUILayout.PropertyField(serializedObject.FindProperty("twoHandBehavior"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("secondaryGrip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pumpSlideTransform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pumpForwardZ"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pumpBackZ"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("OnPumpPulledBack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("OnPumpPushedForward"));

        serializedObject.ApplyModifiedProperties();
    }
}
