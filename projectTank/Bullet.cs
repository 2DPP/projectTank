using System.Drawing;

namespace projectTank
{
    public class Bullet
    {
        public Rectangle Bounds;
        public int Speed;
        public Tank.TankDirection Direction;
        public Tank Owner;
        public bool IsAlive { get; private set; } = true;

        public Bullet(Tank owner, int width, int height, int speed)
        {
            Owner = owner;
            Direction = owner.Direction;
            Speed = speed;

            int centerX = owner.Bounds.X + owner.Bounds.Width / 2 - width / 2;
            int centerY = owner.Bounds.Y + owner.Bounds.Height / 2 - height / 2;

            Bounds = new Rectangle(centerX, centerY, width, height);
        }

        public void Move()
        {
            switch (Direction)
            {
                case Tank.TankDirection.Up: Bounds.Y -= Speed; break;
                case Tank.TankDirection.Down: Bounds.Y += Speed; break;
                case Tank.TankDirection.Left: Bounds.X -= Speed; break;
                case Tank.TankDirection.Right: Bounds.X += Speed; break;
            }
        }

        public void Destroy()
        {
            IsAlive = false;
        }
    }
}