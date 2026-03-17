using System.Collections.Generic; 
using System.Drawing;

// Tank.cs
// Represents a tank in the game. Stores position (Bounds), speed, health,
// direction, and sprite image. Handles movement with wall collision checking
// via Move(), and exposes TakeDamage() and IsAlive for health management.

namespace projectTank // Defines the namespace for organizing related classes
{
    public class Tank // Represents a tank in the game
    {
        public enum TankDirection { Up, Down, Left, Right }
        // Defines possible movement directions for the tank

        public Rectangle Bounds;
        // Stores the position and size of the tank

        public int Speed;
        // Determines how fast the tank moves

        public int Health = 100;
        // Tank's health, starts at 100

        public TankDirection Direction = TankDirection.Up;
        // Current direction of the tank (default is Up)

        public Image TankImage;
        // Image used to visually represent the tank

        public bool IsAlive => Health > 0;
        // Returns true if health is greater than 0, otherwise false

        public bool IsMoving { get; set; } = false;
        // Indicates whether the tank is currently moving

        public Tank(int x, int y, Image img, int width, int height, int speed)
        {
            Bounds = new Rectangle(x, y, width, height);
            // Initializes the tank's position and size

            TankImage = img;
            // Assigns the tank's image

            Speed = speed;
            // Sets the tank's movement speed

            Direction = TankDirection.Up;
            // Sets the default direction to Up
        }

        public void Move(Map map)
        {
            if (!IsMoving) return;
            // If the tank is not moving, exit the method

            Rectangle next = Bounds;
            // Create a copy of the current position to calculate the next move

            // Adjust the next position based on the current direction
            switch (Direction)
            {
                case TankDirection.Up:
                    next.Y -= Speed; // Move up (decrease Y)
                    break;

                case TankDirection.Down:
                    next.Y += Speed; // Move down (increase Y)
                    break;

                case TankDirection.Left:
                    next.X -= Speed; // Move left (decrease X)
                    break;

                case TankDirection.Right:
                    next.X += Speed; // Move right (increase X)
                    break;
            }

            // Check if the next position collides with any wall
            if (!map.IsColliding(next))
                Bounds = next;
            // Only update position if there is no collision
        }

        public void SetDirection(TankDirection newDirection)
        {
            Direction = newDirection;
            // Updates the tank's direction
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            // Reduce health by the given amount

            if (Health < 0) Health = 0;
            // Prevent health from going below 0
        }
    }
}