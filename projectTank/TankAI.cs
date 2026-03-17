using System;
using System.Collections.Generic;
using System.Drawing;

namespace projectTank
{
    // =========================================================================
    // TANK AI
    // Controls the enemy tank automatically each frame.
    // The AI has two main behaviors:
    //   1. MOVEMENT — chases the player while avoiding walls and getting unstuck
    //   2. SHOOTING — fires at the player when they share a row or column
    //                 and there are no walls in between
    // =========================================================================
    public class TankAI
    {
        // =====================================================================
        // REFERENCES
        // The AI needs access to both tanks and the map to make decisions
        // =====================================================================
        private Tank enemy;           // The tank this AI is controlling
        private Tank player;          // The player tank to chase and shoot at
        private Map map;              // The current level map for wall collision checks
        private SoundSystem soundSystem; // Used to trigger movement and shoot sounds

        // =====================================================================
        // RANDOMNESS
        // Used to make movement feel less predictable and robotic
        // =====================================================================
        private Random rng = new Random();

        // =====================================================================
        // TIMERS
        // Control how often decisions are made and how fast the AI can shoot
        // =====================================================================
        private int decisionTimer = 0;  // Counts frames since the last decision
        private int shootCooldown = 0;  // Counts frames since the last shot

        // =====================================================================
        // MOVEMENT STATE
        // Tracks which direction the AI is currently moving
        // =====================================================================
        private Tank.TankDirection moveDirection = Tank.TankDirection.Up;

        // =====================================================================
        // BULLET SETTINGS
        // Passed in from the game so bullet size and speed match the current scale
        // =====================================================================
        private int bulletSize;
        private int bulletSpeed;

        // =====================================================================
        // STUCK DETECTION
        // If the AI hasn't moved for several frames, it is probably stuck against
        // a wall. We track the last position and count stuck frames to detect this.
        // =====================================================================
        private Point lastPosition;
        private int stuckTimer = 0;

        // =====================================================================
        // SHOOT EVENT
        // Instead of adding bullets directly, the AI raises this event.
        // Form1 listens to it and adds the bullet to the game's bullet list.
        // This keeps the AI decoupled from the game's internal state.
        // =====================================================================
        public event Action<Bullet> OnShoot;

        // =====================================================================
        // DIRECTION COMMITMENT
        // The AI commits to a direction for a set number of frames before
        // reconsidering. This prevents it from changing direction every frame,
        // which would make it jitter and look unnatural.
        // =====================================================================
        private int moveCommitTimer = 0;
        private int moveCommitDuration = 60; // Hold a direction for 60 frames (~1 second)

        // =====================================================================
        // BLOCKED DIRECTION MEMORY
        // When a direction leads into a wall, it is remembered as blocked
        // for a short time. This stops the AI from repeatedly trying the same
        // direction and getting stuck in a corner.
        // =====================================================================
        private Dictionary<Tank.TankDirection, int> blockedDirections =
            new Dictionary<Tank.TankDirection, int>();

        private int blockedMemoryTime = 40; // Frames before a blocked direction can be retried

        // =====================================================================
        // CONSTRUCTOR
        // Stores all references needed for movement, shooting, and sound.
        // Records the starting position so stuck detection has a baseline.
        // =====================================================================
        public TankAI(Tank enemyTank, Tank playerTank, int bulletSize, int bulletSpeed, SoundSystem soundSystem)
        {
            this.enemy = enemyTank;
            this.player = playerTank;
            this.bulletSize = bulletSize;
            this.bulletSpeed = bulletSpeed;
            this.soundSystem = soundSystem;

            // Record starting position for stuck detection on the very first frame
            lastPosition = enemy.Bounds.Location;
        }

        // =====================================================================
        // UPDATE
        // Called every frame from the game loop.
        // Runs both movement and shooting logic in order.
        // =====================================================================
        public void Update(Map map)
        {
            this.map = map;

            HandleMovement();  // Decide where to move and move there
            HandleShooting();  // Check if the player can be shot at
        }

        // =====================================================================
        // HANDLE MOVEMENT
        // Each frame, the AI:
        //   1. Counts down blocked direction timers
        //   2. Decides a new direction after the commit period ends
        //   3. Redirects away from walls using shuffled direction attempts
        //   4. Moves the tank and checks if it has become stuck
        // =====================================================================
        private void HandleMovement()
        {
            decisionTimer++;
            moveCommitTimer++;

            // Age all blocked directions — remove them once their timer runs out
            var keys = new List<Tank.TankDirection>(blockedDirections.Keys);
            foreach (var key in keys)
            {
                blockedDirections[key]--;
                if (blockedDirections[key] <= 0)
                    blockedDirections.Remove(key);
            }

            // Only pick a new direction once the commit duration has expired
            if (moveCommitTimer >= moveCommitDuration)
            {
                moveCommitTimer = 0;

                int decision = rng.Next(0, 3); // 0 = wander randomly, 1-2 = chase player

                if (decision == 0)
                {
                    // Random wandering — pick any of the 4 directions at random
                    moveDirection = (Tank.TankDirection)rng.Next(0, 4);
                }
                else
                {
                    // Chase the player — move along whichever axis has the larger gap
                    if (Math.Abs(enemy.Bounds.X - player.Bounds.X) >
                        Math.Abs(enemy.Bounds.Y - player.Bounds.Y))
                    {
                        // Horizontal gap is larger — move left or right toward the player
                        moveDirection = enemy.Bounds.X < player.Bounds.X
                            ? Tank.TankDirection.Right
                            : Tank.TankDirection.Left;
                    }
                    else
                    {
                        // Vertical gap is larger — move up or down toward the player
                        moveDirection = enemy.Bounds.Y < player.Bounds.Y
                            ? Tank.TankDirection.Down
                            : Tank.TankDirection.Up;
                    }
                }
            }

            // WALL AVOIDANCE
            // If the chosen direction leads into a wall, find an alternative
            if (WillHitWall(moveDirection))
            {
                // Mark this direction as blocked so we don't immediately retry it
                blockedDirections[moveDirection] = blockedMemoryTime;

                // Build a list of all 4 directions and shuffle them randomly
                // so the AI doesn't always try the same fallback order
                Tank.TankDirection[] directions =
                {
                    Tank.TankDirection.Up,
                    Tank.TankDirection.Down,
                    Tank.TankDirection.Left,
                    Tank.TankDirection.Right
                };

                // Fisher-Yates shuffle — swaps each element with a random other element
                for (int i = 0; i < directions.Length; i++)
                {
                    int swap = rng.Next(directions.Length);
                    var temp = directions[i];
                    directions[i] = directions[swap];
                    directions[swap] = temp;
                }

                // Pick the first direction that is neither blocked nor hitting a wall
                foreach (var dir in directions)
                {
                    if (!WillHitWall(dir) && !blockedDirections.ContainsKey(dir))
                    {
                        moveDirection = dir;
                        break;
                    }
                }
            }

            // Apply the chosen direction and move the tank
            enemy.SetDirection(moveDirection);
            enemy.IsMoving = true;
            enemy.Move(map);

            soundSystem.UpdateAIMovement(enemy.IsMoving);

            // STUCK DETECTION
            // If the tank's position hasn't changed, it may be wedged against something
            if (enemy.Bounds.Location == lastPosition)
            {
                stuckTimer++;

                // After 20 frames of no movement, force a new clear direction
                if (stuckTimer > 20)
                {
                    Tank.TankDirection[] dirs =
                    {
                        Tank.TankDirection.Up,
                        Tank.TankDirection.Down,
                        Tank.TankDirection.Left,
                        Tank.TankDirection.Right
                    };

                    // Pick the first direction that won't immediately hit a wall
                    foreach (var dir in dirs)
                    {
                        if (!WillHitWall(dir))
                        {
                            moveDirection = dir;
                            break;
                        }
                    }

                    stuckTimer = 0; // Reset so we don't trigger again next frame
                }
            }
            else
            {
                stuckTimer = 0; // Tank moved successfully — it is not stuck
            }

            // Save position this frame to compare against next frame
            lastPosition = enemy.Bounds.Location;
        }

        // =====================================================================
        // HANDLE SHOOTING
        // The AI fires only when two conditions are both true:
        //   1. The player is lined up on the same row or column as the AI
        //   2. No wall blocks the path between the AI and the player
        // A cooldown prevents the AI from firing on every single frame.
        // =====================================================================
        private void HandleShooting()
        {
            shootCooldown++;

            // Enforce a minimum gap between shots
            if (shootCooldown < 50) return;

            // Check alignment — is the player close enough to the same row or column?
            bool sameRow = Math.Abs(enemy.Bounds.Y - player.Bounds.Y) < bulletSize * 2;
            bool sameCol = Math.Abs(enemy.Bounds.X - player.Bounds.X) < bulletSize * 2;

            if (sameRow)
            {
                // Face toward the player horizontally
                enemy.SetDirection(enemy.Bounds.X < player.Bounds.X
                    ? Tank.TankDirection.Right
                    : Tank.TankDirection.Left);

                // Only fire if no wall is in the way
                if (HasLineOfSight())
                {
                    OnShoot?.Invoke(new Bullet(enemy, bulletSize, bulletSize, bulletSpeed));
                    soundSystem.Play("shoot", 0.7f);
                    shootCooldown = 0; // Reset cooldown after firing
                }
            }
            else if (sameCol)
            {
                // Face toward the player vertically
                enemy.SetDirection(enemy.Bounds.Y < player.Bounds.Y
                    ? Tank.TankDirection.Down
                    : Tank.TankDirection.Up);

                // Only fire if no wall is in the way
                if (HasLineOfSight())
                {
                    OnShoot?.Invoke(new Bullet(enemy, bulletSize, bulletSize, bulletSpeed));
                    soundSystem.Play("shoot", 0.7f);
                    shootCooldown = 0;
                }
            }
        }

        // =====================================================================
        // WILL HIT WALL
        // Simulates one step of movement in a given direction and checks
        // whether that position would collide with any wall on the map.
        // Used before moving to safely predict and avoid wall collisions.
        // =====================================================================
        private bool WillHitWall(Tank.TankDirection dir)
        {
            Rectangle next = enemy.Bounds; // Start from the current position

            // Shift the rectangle by one step in the given direction
            switch (dir)
            {
                case Tank.TankDirection.Up: next.Y -= enemy.Speed; break;
                case Tank.TankDirection.Down: next.Y += enemy.Speed; break;
                case Tank.TankDirection.Left: next.X -= enemy.Speed; break;
                case Tank.TankDirection.Right: next.X += enemy.Speed; break;
            }

            return map.IsColliding(next); // True if that position overlaps a wall
        }

        // =====================================================================
        // HAS LINE OF SIGHT
        // Steps an invisible check rectangle forward from the enemy tank
        // in the direction it is facing, one speed unit at a time.
        //
        // Returns true  — the path is clear and the player is reachable
        // Returns false — a wall is blocking the shot, or the check left the map
        //
        // This prevents the AI from shooting through walls.
        // =====================================================================
        private bool HasLineOfSight()
        {
            Rectangle check = enemy.Bounds; // Start the check at the enemy's position

            while (true)
            {
                // Advance the check one step in the firing direction
                switch (enemy.Direction)
                {
                    case Tank.TankDirection.Up: check.Y -= enemy.Speed; break;
                    case Tank.TankDirection.Down: check.Y += enemy.Speed; break;
                    case Tank.TankDirection.Left: check.X -= enemy.Speed; break;
                    case Tank.TankDirection.Right: check.X += enemy.Speed; break;
                }

                // A wall is blocking the path — shot is not possible
                if (map.IsColliding(check))
                    return false;

                // The check reached the player — clear line of sight confirmed
                if (check.IntersectsWith(player.Bounds))
                    return true;

                // The check has left the map entirely — stop to avoid an infinite loop
                if (check.X < 0 || check.Y < 0 || check.X > map.Width || check.Y > map.Height)
                    return false;
            }
        }
    }
}