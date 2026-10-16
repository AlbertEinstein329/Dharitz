using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SessionConfig", menuName = "Dharitz/Session Config")]
public class SessionConfig : ScriptableObject
{
    [Header("Game Mode")]
    public bool isCampaignMode = false;
    [Tooltip("Partida en red: el servidor reparte y valida; playerCount debe ser el numero de jugadores online")]
    public bool isOnlineMatch = false;

    [Header("Selected Rules")]
    public VariantData selectedVariant;

    [Header("Player Settings")]
    public int playerCount = 1;
    public List<PlayerSetup> players = new List<PlayerSetup>();
    [Header("Abilities Settings")]
    public int baseUndoUses = 2;
    public int baseMoveUses = 1;


    // Initialize default list
    public void ResetSession()
    {
        isCampaignMode = false;
        isOnlineMatch = false;
        playerCount = 1;
        baseUndoUses = 2;
        baseMoveUses = 1;
        players.Clear();
        // Prepare 4 slots by default
        for (int i = 0; i < 4; i++)
        {
            players.Add(new PlayerSetup
            {
                playerName = $"Player {i + 1}",
                isBot = false,
                botDifficulty = 0
            });
        }
    }
}

[System.Serializable]
public class PlayerSetup
{
    public string playerName;
    public bool isBot;
    public int botDifficulty;
    public int avatarId;
}