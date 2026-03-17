// PlayerController.cs
// Translates keyboard input (W/A/S/D booleans) into tank movement commands.
// Sets the tank's direction, calls Move() each frame, and triggers the
// movement sound through SoundSystem based on whether the tank is moving.

namespace projectTank // Defines the namespace for organizing related classes
{
    public class PlayerController // Handles player input and movement logic for the tank
    {
        private Tank tank; // Reference to the player's tank
        private SoundSystem soundSystem; // Reference to the sound system for playing audio
        private bool wasMoving = false; // Stores whether the tank was moving in the previous frame

        public PlayerController(Tank tank, SoundSystem soundSystem)
        {
            this.tank = tank; // Assigns the tank object to this controller
            this.soundSystem = soundSystem; // Assigns the sound system
        }

        public void UpdateMovement(bool up, bool down, bool left, bool right, Map map)
        {
            tank.IsMoving = false;
            // Reset movement state before checking input

            // Check input and set direction + movement
            if (up)
            {
                tank.SetDirection(Tank.TankDirection.Up); // Set direction to up
                tank.IsMoving = true; // Mark tank as moving
            }
            else if (down)
            {
                tank.SetDirection(Tank.TankDirection.Down); // Set direction to down
                tank.IsMoving = true;
            }
            else if (left)
            {
                tank.SetDirection(Tank.TankDirection.Left); // Set direction to left
                tank.IsMoving = true;
            }
            else if (right)
            {
                tank.SetDirection(Tank.TankDirection.Right); // Set direction to right
                tank.IsMoving = true;
            }

            tank.Move(map);
            // Move the tank based on direction and check collisions with the map

            // Player movement sound (fixed)
            soundSystem.UpdatePlayerMovement(tank.IsMoving);
            // Plays or stops movement sound depending on whether the tank is moving

            wasMoving = tank.IsMoving;
            // Store current movement state for the next frame
        }
    }
}