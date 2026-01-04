using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI button for activating character abilities
/// Shows cooldown progress and disables when on cooldown
/// Attach this to a UI Button that will trigger abilities
/// </summary>
[RequireComponent(typeof(Button))]
public class AbilityButton : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Cooldown overlay image (fills radially)")]
    [SerializeField] private Image cooldownOverlay;

    [Tooltip("Text showing cooldown time remaining")]
    [SerializeField] private TextMeshProUGUI cooldownText;

    [Tooltip("Icon for the ability")]
    [SerializeField] private Image abilityIcon;

    [Header("Visual Settings")]
    [Tooltip("Color when ability is ready")]
    [SerializeField] private Color readyColor = Color.white;

    [Tooltip("Color when on cooldown")]
    [SerializeField] private Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // References
    private Button button;
    private Ability currentAbility;

    private void Awake()
    {
        button = GetComponent<Button>();

        // Configure cooldown overlay (if assigned)
        if (cooldownOverlay != null)
        {
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
        }
    }

    private void Update()
    {
        // Update cooldown display
        UpdateCooldownDisplay();
    }

    /// <summary>
    /// Set the ability this button controls
    /// Called by PlayerManager when switching characters
    /// </summary>
    public void SetAbility(Ability ability)
    {
        // Unsubscribe from old ability
        if (currentAbility != null)
        {
            currentAbility.OnAbilityActivated -= OnAbilityActivated;
            currentAbility.OnCooldownChanged -= OnCooldownChanged;
        }

        currentAbility = ability;

        // Subscribe to new ability
        if (currentAbility != null)
        {
            currentAbility.OnAbilityActivated += OnAbilityActivated;
            currentAbility.OnCooldownChanged += OnCooldownChanged;

            // Update icon
            if (abilityIcon != null && currentAbility.AbilityData != null && currentAbility.AbilityData.abilityIcon != null)
            {
                abilityIcon.sprite = currentAbility.AbilityData.abilityIcon;
            }

            // Set button label
            if (button != null)
            {
                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null && currentAbility.AbilityData != null)
                {
                    buttonText.text = currentAbility.AbilityData.abilityName.ToUpper();
                }
            }
        }

        UpdateButtonState();
    }

    /// <summary>
    /// Called when button is clicked
    /// </summary>
    public void OnButtonClick()
    {
        if (currentAbility != null)
        {
            currentAbility.TryActivateAbility();
        }
    }

    /// <summary>
    /// Update button interactability based on cooldown
    /// </summary>
    private void UpdateButtonState()
    {
        if (currentAbility == null)
        {
            button.interactable = false;
            return;
        }

        button.interactable = currentAbility.CanUseAbility();
    }

    /// <summary>
    /// Update cooldown visual display
    /// </summary>
    private void UpdateCooldownDisplay()
    {
        if (currentAbility == null) return;

        // Update fill amount
        if (cooldownOverlay != null)
        {
            if (currentAbility.IsOnCooldown)
            {
                cooldownOverlay.fillAmount = 1f - currentAbility.CooldownProgress;
                cooldownOverlay.gameObject.SetActive(true);
            }
            else
            {
                cooldownOverlay.gameObject.SetActive(false);
            }
        }

        // Update cooldown text
        if (cooldownText != null)
        {
            if (currentAbility.IsOnCooldown)
            {
                cooldownText.text = currentAbility.GetFormattedCooldown();
                cooldownText.gameObject.SetActive(true);
            }
            else
            {
                cooldownText.gameObject.SetActive(false);
            }
        }

        // Update button color
        if (abilityIcon != null)
        {
            abilityIcon.color = currentAbility.IsOnCooldown ? cooldownColor : readyColor;
        }

        // Update button state
        UpdateButtonState();
    }

    /// <summary>
    /// Called when ability is activated
    /// </summary>
    private void OnAbilityActivated(Ability ability)
    {
        Debug.Log($"AbilityButton: {ability.AbilityData.abilityName} activated!");
        UpdateButtonState();
    }

    /// <summary>
    /// Called when cooldown changes
    /// </summary>
    private void OnCooldownChanged(float cooldownRemaining, float cooldownProgress)
    {
        // Visual update happens in Update() loop
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (currentAbility != null)
        {
            currentAbility.OnAbilityActivated -= OnAbilityActivated;
            currentAbility.OnCooldownChanged -= OnCooldownChanged;
        }
    }
}
