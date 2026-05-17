using System.Collections.Generic;

[System.Serializable]
public class LevelProgress
{
    public int levelId;
    public int highScore;
    public int stars;
    public bool isUnlocked;

    public LevelProgress(int id, bool unlocked)
    {
        levelId = id;
        highScore = 0;
        stars = 0;
        isUnlocked = unlocked;
    }
}

[System.Serializable]
public class PlayerProfile
{
    public int totalCoins;
    public int maxLevelReached;
    public List<LevelProgress> levels;

    public PlayerProfile()
    {
        totalCoins = 0;
        maxLevelReached = 1;
        levels = new List<LevelProgress>();
        // Nivel 1 desbloqueado por defecto
        levels.Add(new LevelProgress(1, true));
    }

    public LevelProgress GetLevel(int id)
    {
        foreach (var lvl in levels)
        {
            if (lvl.levelId == id)
                return lvl;
        }
        return null;
    }

    public void UnlockNextLevel(int completedLevelId)
    {
        int nextLevelId = completedLevelId + 1;
        var nextLevel = GetLevel(nextLevelId);
        
        if (nextLevel == null)
        {
            levels.Add(new LevelProgress(nextLevelId, true));
        }
        else
        {
            nextLevel.isUnlocked = true;
        }

        if (nextLevelId > maxLevelReached)
        {
            maxLevelReached = nextLevelId;
        }
    }
}
