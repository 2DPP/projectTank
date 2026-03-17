using System.Drawing;
// Allows use of graphics-related classes like Rectangle

namespace projectTank
// Groups related classes together (like a folder)
{
    public class Bullet
    // Represents a bullet fired by a tank
    {
        public Rectangle Bounds;
        // Stores the position and size of the bullet

        public int Speed;
        // Determines how fast the bullet moves

        public Tank.TankDirection Direction;
        // Stores the direction the bullet will travel

        public Tank Owner;
        // Reference to the tank that fired this bullet

        public bool IsAlive { get; private set; } = true;
        // Indicates if the bullet is still active (true = active, false = destroyed)

        public Bullet(Tank owner, int width, int height, int speed)
        // Constructor – called when a new bullet is created
        {
            Owner = owner;
            // Save which tank fired the bullet

            Direction = owner.Direction;
            // Set bullet direction based on the tank's direction

            Speed = speed;
            // Set how fast the bullet moves

            int centerX = owner.Bounds.X + owner.Bounds.Width / 2 - width / 2;
            // Calculate X so the bullet starts at the center of the tank

            int centerY = owner.Bounds.Y + owner.Bounds.Height / 2 - height / 2;
            // Calculate Y so the bullet starts at the center of the tank

            Bounds = new Rectangle(centerX, centerY, width, height);
            // Create the bullet's rectangle using position and size
        }

        public void Move()
        // Moves the bullet each frame
        {
            switch (Direction)
            // Check which direction the bullet is going
            {
                case Tank.TankDirection.Up:
                    Bounds.Y -= Speed;
                    // Move up (decrease Y position)
                    break;

                case Tank.TankDirection.Down:
                    Bounds.Y += Speed;
                    // Move down (increase Y position)
                    break;

                case Tank.TankDirection.Left:
                    Bounds.X -= Speed;
                    // Move left (decrease X position)
                    break;

                case Tank.TankDirection.Right:
                    Bounds.X += Speed;
                    // Move right (increase X position)
                    break;
            }
        }

        public void Destroy()
        // Marks the bullet as no longer active
        {
            IsAlive = false;
            // Bullet is now "dead" and should be removed from the game
        }
    }
}