using System.Collections.Generic;
using System.Drawing;

namespace projectTank
{
    public class Tank
    {
        public enum TankDirection { Up, Down, Left, Right }

        public Rectangle Bounds;
        public int Speed;
        public int Health = 100;
        public TankDirection Direction = TankDirection.Up;
        public Image TankImage;
        public bool IsAlive => Health > 0;
        public bool IsMoving { get; set; } = false;

        public Tank(int x, int y, Image img, int width, int height, int speed)
        {
            Bounds = new Rectangle(x, y, width, height);
            TankImage = img;
            Speed = speed;
            Direction = TankDirection.Up;
        }

        public void Move(Map map)
        {
            if (!IsMoving) return;

            Rectangle next = Bounds;
            switch (Direction)
            {
                case TankDirection.Up: next.Y -= Speed; break;
                case TankDirection.Down: next.Y += Speed; break;
                case TankDirection.Left: next.X -= Speed; break;
                case TankDirection.Right: next.X += Speed; break;
            }

            if (!map.IsColliding(next))
                Bounds = next;
        }

        public void SetDirection(TankDirection newDirection)
        {
            Direction = newDirection;
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            if (Health < 0) Health = 0;
        }
    }
}