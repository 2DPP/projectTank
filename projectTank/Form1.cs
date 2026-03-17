using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using projectTank;

namespace projectTank
{
    public partial class Form1 : Form
    {
        // Game objects
        private Tank player;
        private Tank enemy;
        private TankAI enemyAI;
        private PlayerController playerController;
        private List<Bullet> bullets = new List<Bullet>();
        private List<AnimatedGif> explosions = new List<AnimatedGif>();
        private Map map;

        // Input flags
        private bool up, down, left, right;

        // Scaling factors
        private const int ORIGINAL_WIDTH = 800;
        private const int ORIGINAL_HEIGHT = 600;
        private float scaleX, scaleY;
        private int scaledTankSize;
        private int scaledBulletSize;
        private int scaledTankSpeed;
        private int scaledBulletSpeed;

        // Game loop
        private Timer gameTimer = new Timer();

        // Sounds
        private SoundSystem soundSystem;

        // Player shooting cooldown
        private int playerShootCooldown = 0;
        private const int PLAYER_SHOOT_COOLDOWN_FRAMES = 20;

        // Leveling
        private int level = 1;
        private int maxLevel = 2;
        private bool levelCompleted = false;

        public Form1()
        {
            InitializeComponent();

            // Fullscreen
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;

            // Allow keyboard input
            this.KeyPreview = true;
            this.Focus();
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;

            // Enable double buffering to reduce flicker
            this.DoubleBuffered = true;

            // Scaling based on current screen resolution
            scaleX = (float)this.ClientSize.Width / ORIGINAL_WIDTH;
            scaleY = (float)this.ClientSize.Height / ORIGINAL_HEIGHT;
            float avgScale = (scaleX + scaleY) / 2f;

            scaledTankSize = (int)(50 * avgScale);
            scaledBulletSize = (int)(10 * avgScale);
            scaledTankSpeed = (int)(6 * avgScale);
            scaledBulletSpeed = (int)(15 * avgScale);

            // Initialize SoundSystem
            soundSystem = new SoundSystem();
            
            soundSystem.LoadSound("move", Properties.Resources.TankMove);
            soundSystem.LoadSound("shoot", Properties.Resources.TankShoot);
            soundSystem.LoadSound("explosion", Properties.Resources.Explosion);
            soundSystem.PlayBGM(Properties.Resources.WarBGM);

            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetupGame();

            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }
        private void SetupGame()
        {
            StartLevel(level); // <-- This ensures walls are created and scaled

            playerController = new PlayerController(player, soundSystem);
            enemyAI = new TankAI(enemy, player, scaledBulletSize, scaledBulletSpeed, soundSystem);

            enemyAI.OnShoot += (b) =>
            {
                bullets.Add(b);
                soundSystem.Play("shoot", 0.7f);
            };

            levelCompleted = false;
        }

        private void GameLoop(object sender, EventArgs e)
        {
            bool playerIsMoving = up || down || left || right;
            bool aiIsMoving = enemy.IsMoving;

            soundSystem.UpdatePlayerMovement(playerIsMoving);
            soundSystem.UpdateAIMovement(aiIsMoving);

            playerController.UpdateMovement(up, down, left, right, map);
            enemyAI.Update(map);

            HandleBullets();
            UpdateExplosions();
            CheckGameState();

            if (playerShootCooldown > 0) playerShootCooldown--;

            Invalidate(); // redraw
        }
        private void StartLevel(int lvl)
        {
            int screenWidth = ClientSize.Width;
            int screenHeight = ClientSize.Height;

            // Use screen resolution to compute scaling
            scaleX = (float)screenWidth / ORIGINAL_WIDTH;
            scaleY = (float)screenHeight / ORIGINAL_HEIGHT;
            float scale = Math.Min(scaleX, scaleY); // keep proportions

            // Scaled sizes
            scaledTankSize = (int)(50 * scale);
            scaledBulletSize = (int)(10 * scale);
            scaledTankSpeed = Math.Max(1, (int)(6 * scale));
            scaledBulletSpeed = Math.Max(1, (int)(15 * scale));

            // Initialize map
            map = new Map(screenWidth, screenHeight);

            // Border thickness as 3% of screen height (scaled)
            int borderThickness = Math.Max(10, (int)(screenHeight * 0.03f));

            // Border walls
            map.AddWall(new Rectangle(0, 0, screenWidth, borderThickness)); // top
            map.AddWall(new Rectangle(0, screenHeight - borderThickness, screenWidth, borderThickness)); // bottom
            map.AddWall(new Rectangle(0, 0, borderThickness, screenHeight)); // left
            map.AddWall(new Rectangle(screenWidth - borderThickness, 0, borderThickness, screenHeight)); // right

            // Interior walls based on level
            if (lvl == 2)
            {
                map.AddWall(new Rectangle((int)(screenWidth * 0.25f), (int)(screenHeight * 0.1f),
                                          (int)(screenWidth * 0.05f), (int)(screenHeight * 0.3f)));
                map.AddWall(new Rectangle((int)(screenWidth * 0.5f), 0,
                                          (int)(screenWidth * 0.05f), (int)(screenHeight * 0.25f)));
                map.AddWall(new Rectangle((int)(screenWidth * 0.35f), (int)(screenHeight * 0.6f),
                                          (int)(screenWidth * 0.4f), (int)(screenHeight * 0.05f)));
            }

            // Spawn tanks with scaled size and speed
            player = new Tank(0, 0, Properties.Resources.TankA, scaledTankSize, scaledTankSize, scaledTankSpeed);
            enemy = new Tank(0, 0, Properties.Resources.TankB, scaledTankSize, scaledTankSize, scaledTankSpeed);

            // Place tanks safely
            PlaceTanksSafely();

            // Initialize controllers and AI
            playerController = new PlayerController(player, soundSystem);
            enemyAI = new TankAI(enemy, player, scaledBulletSize, scaledBulletSpeed, soundSystem);
            enemyAI.OnShoot += (b) => { if (b != null) bullets.Add(b); };
        }

        /// <summary>
        /// Safely places tanks randomly without colliding walls
        /// </summary>
        private void PlaceTanksSafely()
        {
            Random rng = new Random();
            int attempts;

            // Player tank
            attempts = 0;
            do
            {
                int x = rng.Next(scaledTankSize, ClientSize.Width - scaledTankSize);
                int y = rng.Next(scaledTankSize, ClientSize.Height - scaledTankSize);
                player.Bounds = new Rectangle(x, y, scaledTankSize, scaledTankSize);
                attempts++;
            } while (map.IsColliding(player.Bounds) && attempts < 100);

            // Enemy tank
            attempts = 0;
            do
            {
                int x = rng.Next(scaledTankSize, ClientSize.Width - scaledTankSize);
                int y = rng.Next(scaledTankSize, ClientSize.Height - scaledTankSize);
                enemy.Bounds = new Rectangle(x, y, scaledTankSize, scaledTankSize);
                attempts++;
            } while (map.IsColliding(enemy.Bounds) && attempts < 100);
        }
        // Key handling
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W: up = true; break;
                case Keys.S: down = true; break;
                case Keys.A: left = true; break;
                case Keys.D: right = true; break;
                case Keys.Space:
                    if (playerShootCooldown <= 0)
                    {
                        bullets.Add(new Bullet(player, scaledBulletSize, scaledBulletSize, scaledBulletSpeed));
                        soundSystem.Play("shoot", 0.7f);
                        playerShootCooldown = PLAYER_SHOOT_COOLDOWN_FRAMES;
                    }
                    break;
                case Keys.Escape: Close(); break;
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W: up = false; break;
                case Keys.S: down = false; break;
                case Keys.A: left = false; break;
                case Keys.D: right = false; break;
            }
        }
        class SmokeParticle
        {
            public float X, Y;
            public float Size;
            public float Alpha;
            public float Life;
            public float MaxLife;

            public SmokeParticle(float x, float y)
            {
                X = x;
                Y = y;
                Size = 6f;
                Alpha = 1f;
                Life = 0f;
                MaxLife = 500f; // milliseconds
            }
        }
        private class AnimatedGif
        {
            public Image Img;
            public Rectangle Bounds;
            public int AgeMs;
            public int DurationMs;
            public EventHandler FrameChangedHandler;
            public AnimatedGif(Image img, Rectangle bounds, int durationMs = 1000)
            {
                Img = img;
                Bounds = bounds;
                AgeMs = 0;
                DurationMs = durationMs;
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Draw map walls safely
            if (map != null && map.Walls != null)
            {
                foreach (var wall in map.Walls)
                {
                    if (wall != null)
                        wall.Draw(g);
                }
            }

            // Draw tanks safely
            if (player != null && player.TankImage != null)
                DrawTank(g, player);
            if (enemy != null && enemy.TankImage != null)
                DrawTank(g, enemy);

            // Draw bullets safely
            foreach (var b in bullets.ToList())
            {
                if (b != null)
                    g.FillEllipse(Brushes.Yellow, b.Bounds);
            }

            // Draw active explosion GIFs
            if (explosions != null)
            {
                foreach (var ex in explosions.ToList())
                {
                    try { ImageAnimator.UpdateFrames(ex.Img); } catch { }
                    if (ex.Img != null)
                        g.DrawImage(ex.Img, ex.Bounds);
                }
            }



            // Draw health bars safely
            if (player != null)
                DrawHealthBar(g, player);
            if (enemy != null)
                DrawHealthBar(g, enemy);

            base.OnPaint(e);
        }

        private void DrawTank(Graphics g, Tank tank)
        {
            float angle = 0;
            switch (tank.Direction)
            {
                case Tank.TankDirection.Up: angle = 0; break;
                case Tank.TankDirection.Right: angle = 90; break;
                case Tank.TankDirection.Down: angle = 180; break;
                case Tank.TankDirection.Left: angle = 270; break;
            }

            g.TranslateTransform(tank.Bounds.X + tank.Bounds.Width / 2,
                                 tank.Bounds.Y + tank.Bounds.Height / 2);
            g.RotateTransform(angle);
            g.DrawImage(tank.TankImage, -tank.Bounds.Width / 2, -tank.Bounds.Height / 2,
                        tank.Bounds.Width, tank.Bounds.Height);
            g.ResetTransform();
        }

        private void DrawHealthBar(Graphics g, Tank tank)
        {
            int barWidth = tank.Bounds.Width;
            int barHeight = 6;
            int healthWidth = (int)(barWidth * (tank.Health / 100.0));

            g.FillRectangle(Brushes.DarkRed, tank.Bounds.X, tank.Bounds.Y - 10, barWidth, barHeight);
            g.FillRectangle(Brushes.LimeGreen, tank.Bounds.X, tank.Bounds.Y - 10, healthWidth, barHeight);
        }

        private void HandleBullets()
        {
            foreach (var bullet in bullets.ToList())
            {
                bullet.Move();

                if (bullet.Bounds.X < 0 || bullet.Bounds.Y < 0 ||
                    bullet.Bounds.X > ClientSize.Width || bullet.Bounds.Y > ClientSize.Height)
                {
                    bullets.Remove(bullet);
                    continue;
                }

                if (bullet.Owner == player && bullet.Bounds.IntersectsWith(enemy.Bounds))
                {
                    enemy.TakeDamage(10);
                    // create explosion gif at enemy location
                    CreateExplosion(enemy.Bounds);
                    bullets.Remove(bullet);
                    soundSystem.Play("explosion", 0.7f);
                    continue;
                }

                if (bullet.Owner == enemy && bullet.Bounds.IntersectsWith(player.Bounds))
                {
                    player.TakeDamage(10);
                    // create explosion gif at player location proportional to tank size
                    CreateExplosion(player.Bounds);
                    bullets.Remove(bullet);
                    soundSystem.Play("explosion", 0.7f);
                    continue;
                }

                if (map.IsColliding(bullet.Bounds))
                {
                    // create explosion gif at collision location (bullet)
                    CreateExplosion(bullet.Bounds);
                    bullets.Remove(bullet);
                    soundSystem.Play("explosion", 0.7f);
                }
            }
        }

        /// <summary>
        /// Create an animated gif explosion at the given bounds.
        /// </summary>
        private void CreateExplosion(Rectangle bounds)
        {
            try
            {
                // Load GIF from Resources instead of file path
                Image img = Properties.Resources.ExplosionFx;

                // Start animating
                EventHandler handler = new EventHandler(OnGifFrameChanged);
                ImageAnimator.Animate(img, handler);

                // Scale explosion based on object size
                int baseSize = Math.Max(bounds.Width, bounds.Height);
                int w = Math.Max(32, (int)(baseSize * 1.2));
                int h = Math.Max(32, (int)(baseSize * 1.2));

                Rectangle drawBounds = new Rectangle(
                    bounds.X + (bounds.Width - w) / 2,
                    bounds.Y + (bounds.Height - h) / 2,
                    w,
                    h
                );

                var ag = new AnimatedGif(img, drawBounds, 1000);
                ag.FrameChangedHandler = handler;

                explosions.Add(ag);
            }
            catch
            {
                // ignore errors
            }
        }

        private void OnGifFrameChanged(object sender, EventArgs e)
        {
            // ensure the form repaints when the animated gif advances frames
            Invalidate();
        }

        private void UpdateExplosions()
        {
            if (explosions == null || explosions.Count == 0) return;

            foreach (var ex in explosions.ToList())
            {
                // advance animated gif frames
                try { ImageAnimator.UpdateFrames(ex.Img); } catch { }

                ex.AgeMs += gameTimer.Interval;
                if (ex.AgeMs > ex.DurationMs)
                {
                    try { if (ex.FrameChangedHandler != null) ImageAnimator.StopAnimate(ex.Img, ex.FrameChangedHandler); } catch { }
                    try { ex.Img.Dispose(); } catch { }
                    explosions.Remove(ex);
                }
            }
        }

        private void CheckGameState()
        {
            if (!player.IsAlive)
            {
                gameTimer.Stop();
                MessageBox.Show("You Lose!");
                Close();
                return;
            }

            if (!enemy.IsAlive)
            {
                gameTimer.Stop(); // temporarily stop updates

                MessageBox.Show($"Level {level} Complete!");

                level++; // go to next level
                if (level > maxLevel)
                {
                    MessageBox.Show("You completed all levels!");
                    Close();
                    return;
                }

                // Clear previous bullets
                bullets.Clear();

                // Start next level
                StartLevel(level);

                // Reset cooldowns etc.
                playerShootCooldown = 0;

                // Restart the timer
                gameTimer.Start();
            }
        }
    }
}