using System;

namespace projectTank
{
    public class TankAI
    {
        private Tank enemy;
        private Tank player;
        private Map map;
        private SoundSystem soundSystem;
        private bool wasMoving = false;

        private Random rng = new Random();
        private int decisionTimer = 0;
        private int shootCooldown = 0;
        private Tank.TankDirection moveDirection = Tank.TankDirection.Up;
        private int bulletSize;
        private int bulletSpeed;

        public event Action<Bullet> OnShoot;

        public TankAI(Tank enemyTank, Tank playerTank, int bulletSize, int bulletSpeed, SoundSystem soundSystem)
        {
            this.enemy = enemyTank;
            this.player = playerTank;
            this.bulletSize = bulletSize;
            this.bulletSpeed = bulletSpeed;
            this.soundSystem = soundSystem;
        }

        public void Update(Map map)
        {
            this.map = map;

            HandleMovement();
            HandleShooting();
        }

        private void HandleMovement()
        {
            decisionTimer++;
            if (decisionTimer >= 40)
            {
                decisionTimer = 0;
                int decision = rng.Next(0, 3);
                if (decision == 0) // random
                    moveDirection = (Tank.TankDirection)rng.Next(0, 4);
                else // chase player
                {
                    if (Math.Abs(enemy.Bounds.X - player.Bounds.X) > Math.Abs(enemy.Bounds.Y - player.Bounds.Y))
                        moveDirection = enemy.Bounds.X < player.Bounds.X ? Tank.TankDirection.Right : Tank.TankDirection.Left;
                    else
                        moveDirection = enemy.Bounds.Y < player.Bounds.Y ? Tank.TankDirection.Down : Tank.TankDirection.Up;
                }
            }

            enemy.SetDirection(moveDirection);
            enemy.IsMoving = true;
            enemy.Move(map);

            // AI movement sound (fixed)
            soundSystem.UpdateAIMovement(enemy.IsMoving);

            wasMoving = enemy.IsMoving;
        }

        private void HandleShooting()
        {
            shootCooldown++;
            if (shootCooldown < 50) return;

            bool sameRow = Math.Abs(enemy.Bounds.Y - player.Bounds.Y) < bulletSize * 2;
            bool sameCol = Math.Abs(enemy.Bounds.X - player.Bounds.X) < bulletSize * 2;

            if (sameRow)
            {
                enemy.SetDirection(enemy.Bounds.X < player.Bounds.X ? Tank.TankDirection.Right : Tank.TankDirection.Left);
                OnShoot?.Invoke(new Bullet(enemy, bulletSize, bulletSize, bulletSpeed));
                soundSystem.Play("shoot", 0.7f);
                shootCooldown = 0;
            }
            else if (sameCol)
            {
                enemy.SetDirection(enemy.Bounds.Y < player.Bounds.Y ? Tank.TankDirection.Down : Tank.TankDirection.Up);
                OnShoot?.Invoke(new Bullet(enemy, bulletSize, bulletSize, bulletSpeed));
                soundSystem.Play("shoot", 0.7f);
                shootCooldown = 0;
            }
        }
    }
}