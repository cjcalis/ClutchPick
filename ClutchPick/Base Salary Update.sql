-- Step 1: Rank players within each league
WITH RankedPlayers AS (
    SELECT 
        PlayerID,
        League,
        Rating,
        RANK() OVER (PARTITION BY League ORDER BY Rating DESC) AS RankInLeague
    FROM LeaguePlayers
),

-- Step 2: Get the max rank per league
MaxRanks AS (
    SELECT 
        League,
        MAX(RankInLeague) AS MaxRank
    FROM RankedPlayers
    GROUP BY League
)

-- Step 3: Update salaries
UPDATE lp
SET Salary = 40000 + ((mr.MaxRank - rp.RankInLeague + 1) * 5000)
FROM LeaguePlayers lp
JOIN RankedPlayers rp ON lp.PlayerID = rp.PlayerID
JOIN MaxRanks mr ON rp.League = mr.League;
