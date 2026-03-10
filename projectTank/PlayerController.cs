namespace projectTank
{
    public class PlayerController
    {
        private Tank tank;
        private SoundSystem soundSystem;
        private bool wasMoving = false; // previous frame

        public PlayerController(Tank tank, SoundSystem soundSystem)
        {
            this.tank = tank;
            this.soundSystem = soundSystem;
        }

        public void UpdateMovement(bool up, bool down, bool left, bool right, Map map)
        {
            tank.IsMoving = false;

            if (up) { tank.SetDirection(Tank.TankDirection.Up); tank.IsMoving = true; }
            else if (down) { tank.SetDirection(Tank.TankDirection.Down); tank.IsMoving = true; }
            else if (left) { tank.SetDirection(Tank.TankDirection.Left); tank.IsMoving = true; }
            else if (right) { tank.SetDirection(Tank.TankDirection.Right); tank.IsMoving = true; }

            tank.Move(map);

            // Player movement sound (fixed)
            soundSystem.UpdatePlayerMovement(tank.IsMoving);

            wasMoving = tank.IsMoving;
        }
    }
}