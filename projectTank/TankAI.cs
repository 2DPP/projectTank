using System;
using System.Collections.Generic;
using System.Drawing;


namespace projectTank
{
    public class TankAI
    {
        // Reference to the enemy tank controlled by AI
        private Tank enemy;

        // Reference to the player tank so the AI can chase or shoot it
        private Tank player;

        // Reference to the map to detect wall collisions
        private Map map;

        // Sound system for movement and shooting sounds
        private SoundSystem soundSystem;

        // Random generator used for random movement decisions
        private Random rng = new Random();

        // Timer controlling how often the AI changes decisions
        private int decisionTimer = 0;

        // Cooldown timer for shooting so AI does not spam bullets
        private int shootCooldown = 0;

        // Current movement direction of the AI
        private Tank.TankDirection moveDirection = Tank.TankDirection.Up;

        // Bullet settings passed from the game
        private int bulletSize;
        private int bulletSpeed;

        // Variables used to detect if the AI tank becomes stuck
        private Point lastPosition;
        private int stuckTimer = 0;

        // Event used to notify the game when the AI fires a bullet
        public event Action<Bullet> OnShoot;

        private int moveCommitTimer = 0;
        private int moveCommitDuration = 60; // frames to keep moving in one direction

        // Remember directions that recently failed
        private Dictionary<Tank.TankDirection, int> blockedDirections =
            new Dictionary<Tank.TankDirection, int>();

        private int blockedMemoryTime = 40; // frames before direction can be tried again
        public TankAI(Tank enemyTank, Tank playerTank, int bulletSize, int bulletSpeed, SoundSystem soundSystem)
        {
            this.enemy = enemyTank;
            this.player = playerTank;
            this.bulletSize = bulletSize;
            this.bulletSpeed = bulletSpeed;
            this.soundSystem = soundSystem;

            // Store initial position so we can later detect if the tank is stuck
            lastPosition = enemy.Bounds.Location;
        }

        // Called every game update
        public void Update(Map map)
        {
            this.map = map;

            HandleMovement();
            HandleShooting();
        }

        private void HandleMovement()
        {
            decisionTimer++;
            moveCommitTimer++;
            // Reduce blocked direction timers
            var keys = new List<Tank.TankDirection>(blockedDirections.Keys);
            foreach (var key in keys)
            {
                blockedDirections[key]--;

                if (blockedDirections[key] <= 0)
                    blockedDirections.Remove(key);
            }
            // Only allow changing direction after commitment period
            if (moveCommitTimer >= moveCommitDuration)
            {
                moveCommitTimer = 0;

                int decision = rng.Next(0, 3);

                if (decision == 0)
                {
                    // Random wandering
                    moveDirection = (Tank.TankDirection)rng.Next(0, 4);
                }
                else
                {
                    // Chase player
                    if (Math.Abs(enemy.Bounds.X - player.Bounds.X) >
                        Math.Abs(enemy.Bounds.Y - player.Bounds.Y))
                    {
                        moveDirection = enemy.Bounds.X < player.Bounds.X
                            ? Tank.TankDirection.Right
                            : Tank.TankDirection.Left;
                    }
                    else
                    {
                        moveDirection = enemy.Bounds.Y < player.Bounds.Y
                            ? Tank.TankDirection.Down
                            : Tank.TankDirection.Up;
                    }
                }
            }

            // WALL AVOIDANCE
            if (WillHitWall(moveDirection))
            {
                // Remember this direction is blocked
                blockedDirections[moveDirection] = blockedMemoryTime;

                Tank.TankDirection[] directions =
                {
                  Tank.TankDirection.Up,
                   Tank.TankDirection.Down,
                     Tank.TankDirection.Left,
                        Tank.TankDirection.Right
                };

                // Shuffle directions
                for (int i = 0; i < directions.Length; i++)
                {
                    int swap = rng.Next(directions.Length);
                    var temp = directions[i];
                    directions[i] = directions[swap];
                    directions[swap] = temp;
                }

                foreach (var dir in directions)
                {
                    if (!WillHitWall(dir) && !blockedDirections.ContainsKey(dir))
                    {
                        moveDirection = dir;
                        break;
                    }
                }
            }

            enemy.SetDirection(moveDirection);
            enemy.IsMoving = true;
            enemy.Move(map);

            soundSystem.UpdateAIMovement(enemy.IsMoving);

            // UNSTUCK DETECTION
            if (enemy.Bounds.Location == lastPosition)
            {
                stuckTimer++;

                if (stuckTimer > 20)
                {
                    // Force escape direction
                    Tank.TankDirection[] dirs =
                    {
                Tank.TankDirection.Up,
                Tank.TankDirection.Down,
                Tank.TankDirection.Left,
                Tank.TankDirection.Right
            };

                    foreach (var dir in dirs)
                    {
                        if (!WillHitWall(dir))
                        {
                            moveDirection = dir;
                            break;
                        }
                    }

                    stuckTimer = 0;
                }
            }
            else
            {
                stuckTimer = 0;
            }

            lastPosition = enemy.Bounds.Location;
        }

        private void HandleShooting()
        {
            shootCooldown++;

            // Prevent the AI from shooting too frequently
            if (shootCooldown < 50) return;

            // Check if the player is roughly aligned horizontally or vertically
            bool sameRow = Math.Abs(enemy.Bounds.Y - player.Bounds.Y) < bulletSize * 2;
            bool sameCol = Math.Abs(enemy.Bounds.X - player.Bounds.X) < bulletSize * 2;

            if (sameRow)
            {
                enemy.SetDirection(enemy.Bounds.X < player.Bounds.X ?
                    Tank.TankDirection.Right :
                    Tank.TankDirection.Left);

                if (HasLineOfSight())
                {
                    OnShoot?.Invoke(new Bullet(enemy, bulletSize, bulletSize, bulletSpeed));
                    soundSystem.Play("shoot", 0.7f);
                    shootCooldown = 0;
                }
            }
            else if (sameCol)
            {
                enemy.SetDirection(enemy.Bounds.Y < player.Bounds.Y ?
                    Tank.TankDirection.Down :
                    Tank.TankDirection.Up);

                if (HasLineOfSight())
                {
                    OnShoot?.Invoke(new Bullet(enemy, bulletSize, bulletSize, bulletSpeed));
                    soundSystem.Play("shoot", 0.7f);
                    shootCooldown = 0;
                }
            }
        }

        // Predicts whether the tank will collide with a wall
        // if it moves in a given direction
        private bool WillHitWall(Tank.TankDirection dir)
        {
            Rectangle next = enemy.Bounds;

            switch (dir)
            {
                case Tank.TankDirection.Up:
                    next.Y -= enemy.Speed;
                    break;

                case Tank.TankDirection.Down:
                    next.Y += enemy.Speed;
                    break;

                case Tank.TankDirection.Left:
                    next.X -= enemy.Speed;
                    break;

                case Tank.TankDirection.Right:
                    next.X += enemy.Speed;
                    break;
            }

            return map.IsColliding(next);
        }
        // Checks if a wall blocks the shot between enemy and player
        private bool HasLineOfSight()
        {
            Rectangle check = enemy.Bounds;

            while (true)
            {
                switch (enemy.Direction)
                {
                    case Tank.TankDirection.Up:
                        check.Y -= enemy.Speed;
                        break;

                    case Tank.TankDirection.Down:
                        check.Y += enemy.Speed;
                        break;

                    case Tank.TankDirection.Left:
                        check.X -= enemy.Speed;
                        break;

                    case Tank.TankDirection.Right:
                        check.X += enemy.Speed;
                        break;
                }

                // If we hit a wall, the shot is blocked
                if (map.IsColliding(check))
                    return false;

                // If we reach the player, the shot is clear
                if (check.IntersectsWith(player.Bounds))
                    return true;

                // Stop checking if we go outside the map
                if (check.X < 0 || check.Y < 0 || check.X > map.Width || check.Y > map.Height)
                    return false;
            }
        }
    }
}