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
        private List<SmokeParticle> smokeParticles = new List<SmokeParticle>();
        private Map map;
        private List<GifFrame> explosionFrames = new List<GifFrame>();
        private Random rng = new Random();

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

        // Real elapsed time tracking
        private System.Diagnostics.Stopwatch frameStopwatch = new System.Diagnostics.Stopwatch();
        private int actualElapsed = 16;

        // Sounds
        private SoundSystem soundSystem;

        // Player shooting cooldown
        private int playerShootCooldown = 0;
        private const int PLAYER_SHOOT_COOLDOWN_FRAMES = 20;

        // Smoke particle cap
        private const int MAX_SMOKE_PARTICLES = 100;

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

            // Show a simple loading label while frames load
            Label loadingLabel = new Label();
            loadingLabel.Text = "Loading...";
            loadingLabel.ForeColor = Color.White;
            loadingLabel.BackColor = Color.Black;
            loadingLabel.Font = new Font("Arial", 24, FontStyle.Bold);
            loadingLabel.AutoSize = true;
            loadingLabel.Location = new Point(ClientSize.Width / 2 - 60, ClientSize.Height / 2 - 20);
            this.Controls.Add(loadingLabel);
            loadingLabel.BringToFront();

            // Load GIF frames on a background thread
            System.Threading.Tasks.Task.Run(() =>
            {
                LoadGifFrames(Properties.Resources.ExplosionFx);

            }).ContinueWith(t =>
            {
                // Back on UI thread — remove label and start game
                this.Controls.Remove(loadingLabel);
                loadingLabel.Dispose();

                gameTimer.Interval = 16;
                gameTimer.Tick += GameLoop;
                frameStopwatch.Restart();
                gameTimer.Start();

            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SetupGame()
        {
            StartLevel(level);

            playerController = new PlayerController(player, soundSystem);
            enemyAI = new TankAI(enemy, player, scaledBulletSize, scaledBulletSpeed, soundSystem);

            enemyAI.OnShoot += (b) =>
            {
                if (b != null)
                {
                    bullets.Add(b);
                    soundSystem.Play("shoot", 0.7f);
                }
            };

            levelCompleted = false;
        }

        private void GameLoop(object sender, EventArgs e)
        {
            // Measure real elapsed time
            actualElapsed = (int)frameStopwatch.ElapsedMilliseconds;
            if (actualElapsed < 1) actualElapsed = 1;
            if (actualElapsed > 100) actualElapsed = 100;
            frameStopwatch.Restart();

            bool playerIsMoving = up || down || left || right;
            bool aiIsMoving = enemy.IsMoving;

            soundSystem.UpdatePlayerMovement(playerIsMoving);
            soundSystem.UpdateAIMovement(aiIsMoving);

            playerController.UpdateMovement(up, down, left, right, map);
            enemyAI.Update(map);

            HandleBullets();
            UpdateExplosions();
            CheckGameState();
            UpdateSmoke();

            if (playerShootCooldown > 0) playerShootCooldown--;

            Invalidate();
        }

        private void StartLevel(int lvl)
        {
            int screenWidth = ClientSize.Width;
            int screenHeight = ClientSize.Height;

            scaleX = (float)screenWidth / ORIGINAL_WIDTH;
            scaleY = (float)screenHeight / ORIGINAL_HEIGHT;
            float scale = Math.Min(scaleX, scaleY);

            scaledTankSize = (int)(50 * scale);
            scaledBulletSize = (int)(10 * scale);
            scaledTankSpeed = Math.Max(1, (int)(6 * scale));
            scaledBulletSpeed = Math.Max(1, (int)(15 * scale));

            map = new Map(screenWidth, screenHeight);

            int borderThickness = Math.Max(10, (int)(screenHeight * 0.03f));

            map.AddWall(new Rectangle(0, 0, screenWidth, borderThickness));
            map.AddWall(new Rectangle(0, screenHeight - borderThickness, screenWidth, borderThickness));
            map.AddWall(new Rectangle(0, 0, borderThickness, screenHeight));
            map.AddWall(new Rectangle(screenWidth - borderThickness, 0, borderThickness, screenHeight));

            if (lvl == 2)
            {
                map.AddWall(new Rectangle((int)(screenWidth * 0.25f), (int)(screenHeight * 0.1f),
                                          (int)(screenWidth * 0.05f), (int)(screenHeight * 0.3f)));
                map.AddWall(new Rectangle((int)(screenWidth * 0.5f), 0,
                                          (int)(screenWidth * 0.05f), (int)(screenHeight * 0.25f)));
                map.AddWall(new Rectangle((int)(screenWidth * 0.35f), (int)(screenHeight * 0.6f),
                                          (int)(screenWidth * 0.4f), (int)(screenHeight * 0.05f)));
            }

            player = new Tank(0, 0, Properties.Resources.TankA, scaledTankSize, scaledTankSize, scaledTankSpeed);
            enemy = new Tank(0, 0, Properties.Resources.TankB, scaledTankSize, scaledTankSize, scaledTankSpeed);

            PlaceTanksSafely();

            playerController = new PlayerController(player, soundSystem);
            enemyAI = new TankAI(enemy, player, scaledBulletSize, scaledBulletSpeed, soundSystem);
            enemyAI.OnShoot += (b) => { if (b != null) bullets.Add(b); };
        }

         void LoadGifFrames(Image gif)
        {
            var frames = new List<GifFrame>();

            FrameDimension dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(dimension);

            PropertyItem delayItem = null;
            try { delayItem = gif.GetPropertyItem(0x5100); } catch { }

            for (int i = 0; i < frameCount; i++)
            {
                gif.SelectActiveFrame(dimension, i);

                // Clone to a new Bitmap so it's independent of the GIF stream
                Bitmap frame = new Bitmap(gif.Width, gif.Height, PixelFormat.Format32bppArgb);
                using (Graphics fg = Graphics.FromImage(frame))
                    fg.DrawImage(gif, 0, 0, gif.Width, gif.Height);

                int delay = 100;
                if (delayItem != null && i * 4 + 3 < delayItem.Value.Length)
                {
                    delay = BitConverter.ToInt32(delayItem.Value, i * 4) * 10;
                    if (delay <= 0) delay = 100;
                }

                frames.Add(new GifFrame { Image = frame, Delay = delay });
            }

            // Assign back on completion — safe since game hasn't started yet
            explosionFrames = frames;
        }
        private void PlaceTanksSafely()
        {
            Random rng = new Random();
            int attempts;

            attempts = 0;
            do
            {
                int x = rng.Next(scaledTankSize, ClientSize.Width - scaledTankSize);
                int y = rng.Next(scaledTankSize, ClientSize.Height - scaledTankSize);
                player.Bounds = new Rectangle(x, y, scaledTankSize, scaledTankSize);
                attempts++;
            } while (map.IsColliding(player.Bounds) && attempts < 100);

            attempts = 0;
            do
            {
                int x = rng.Next(scaledTankSize, ClientSize.Width - scaledTankSize);
                int y = rng.Next(scaledTankSize, ClientSize.Height - scaledTankSize);
                enemy.Bounds = new Rectangle(x, y, scaledTankSize, scaledTankSize);
                attempts++;
            } while (map.IsColliding(enemy.Bounds) && attempts < 100);
        }

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
            public float VelX, VelY;

            public SmokeParticle(float x, float y, Random rng)
            {
                X = x;
                Y = y;
                Size = rng.Next(4, 8);
                Alpha = 1f;
                Life = 0f;
                MaxLife = rng.Next(300, 500);
                VelX = (float)(rng.NextDouble() - 0.5) * 0.5f;
                VelY = (float)(-0.3 - rng.NextDouble() * 0.5);
            }
        }

        private void UpdateSmoke()
        {
            for (int i = smokeParticles.Count - 1; i >= 0; i--)
            {
                var p = smokeParticles[i];
                p.Life += actualElapsed;

                float lifeRatio = p.Life / p.MaxLife;
                p.Alpha = 1f - lifeRatio;
                p.Size += 0.3f;
                p.X += p.VelX;
                p.Y += p.VelY;

                if (p.Life >= p.MaxLife)
                    smokeParticles.RemoveAt(i);
            }
        }

        class GifFrame
        {
            public Image Image;
            public int Delay;
        }

        class AnimatedGif
        {
            public Rectangle Bounds;
            public int CurrentFrame = 0;
            public int Timer = 0;
            public float Alpha = 1f;
            public int TotalElapsed = 0;
            public bool Finished = false;
            public const int MAX_LIFETIME = 1200; // hard cap in ms

            public AnimatedGif(Rectangle bounds)
            {
                Bounds = bounds;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Draw map walls
            if (map != null && map.Walls != null)
            {
                foreach (var wall in map.Walls)
                {
                    if (wall != null)
                        wall.Draw(g);
                }
            }

            // Draw tanks
            if (player != null && player.TankImage != null)
                DrawTank(g, player);
            if (enemy != null && enemy.TankImage != null)
                DrawTank(g, enemy);

            // Draw bullets
            foreach (var b in bullets.ToList())
            {
                if (b != null)
                    g.FillEllipse(Brushes.Yellow, b.Bounds);
            }

            // Draw smoke particles
            foreach (var p in smokeParticles.ToList())
            {
                int alpha = (int)(p.Alpha * 255);
                if (alpha <= 0) continue;
                if (alpha > 255) alpha = 255;

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 100, 100, 100)))
                {
                    g.FillEllipse(brush, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
                }
            }

            // Draw explosions
            foreach (var ex in explosions.ToList())
            {
                if (ex.Finished) continue;
                if (ex.CurrentFrame < 0 || ex.CurrentFrame >= explosionFrames.Count) continue;

                var frame = explosionFrames[ex.CurrentFrame];
                float alpha = Math.Max(0f, Math.Min(1f, ex.Alpha));

                if (alpha <= 0f) continue;

                if (alpha >= 1f)
                {
                    // No transparency needed — simple fast draw
                    g.DrawImage(frame.Image, ex.Bounds);
                }
                else
                {
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = alpha;
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(matrix);

                    g.DrawImage(
                        frame.Image,
                        ex.Bounds,
                        0, 0, frame.Image.Width, frame.Image.Height,
                        GraphicsUnit.Pixel,
                        attributes
                    );

                    attributes.Dispose();
                }
            }

            // Draw health bars
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
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                bullet.Move();

                // Spawn smoke trail only if under cap
                if (smokeParticles.Count < MAX_SMOKE_PARTICLES)
                {
                    float offsetX = rng.Next(-2, 3);
                    float offsetY = rng.Next(-2, 3);

                    smokeParticles.Add(new SmokeParticle(
                        bullet.Bounds.X + bullet.Bounds.Width / 2 + offsetX,
                        bullet.Bounds.Y + bullet.Bounds.Height / 2 + offsetY,
                        rng
                    ));
                }

                // Out of bounds
                if (bullet.Bounds.X < 0 || bullet.Bounds.Y < 0 ||
                    bullet.Bounds.X > ClientSize.Width || bullet.Bounds.Y > ClientSize.Height)
                {
                    bullets.RemoveAt(i);
                    continue;
                }

                // Player bullet hits enemy
                if (bullet.Owner == player && bullet.Bounds.IntersectsWith(enemy.Bounds))
                {
                    enemy.TakeDamage(10);
                    CreateExplosion(enemy.Bounds);
                    bullets.RemoveAt(i);
                    soundSystem.Play("explosion", 0.7f);
                    continue;
                }

                // Enemy bullet hits player
                if (bullet.Owner == enemy && bullet.Bounds.IntersectsWith(player.Bounds))
                {
                    player.TakeDamage(10);
                    CreateExplosion(player.Bounds);
                    bullets.RemoveAt(i);
                    soundSystem.Play("explosion", 0.7f);
                    continue;
                }

                // Bullet hits wall
                if (map.IsColliding(bullet.Bounds))
                {
                    CreateExplosion(bullet.Bounds);
                    bullets.RemoveAt(i);
                    soundSystem.Play("explosion", 0.7f);
                }
            }
        }

        private void CreateExplosion(Rectangle bounds)
        {
            int baseSize = Math.Max(bounds.Width, bounds.Height);
            int size = Math.Max(32, (int)(baseSize * 1.2));

            Rectangle drawBounds = new Rectangle(
                bounds.X + (bounds.Width - size) / 2,
                bounds.Y + (bounds.Height - size) / 2,
                size,
                size
            );

            explosions.Add(new AnimatedGif(drawBounds));
        }

        private void UpdateExplosions()
        {
            for (int i = explosions.Count - 1; i >= 0; i--)
            {
                var ex = explosions[i];
                ex.Timer += actualElapsed;
                ex.TotalElapsed += actualElapsed;

                // Hard timeout — always remove no matter what
                if (ex.TotalElapsed >= AnimatedGif.MAX_LIFETIME)
                {
                    explosions.RemoveAt(i);
                    continue;
                }

                // Already done
                if (ex.Finished)
                {
                    explosions.RemoveAt(i);
                    continue;
                }

                // Safety: frame out of range
                if (ex.CurrentFrame >= explosionFrames.Count)
                {
                    ex.Finished = true;
                    explosions.RemoveAt(i);
                    continue;
                }

                // Advance frame when delay is met
                if (ex.Timer >= explosionFrames[ex.CurrentFrame].Delay)
                {
                    ex.Timer = 0;
                    ex.CurrentFrame++;

                    // Past last frame — done
                    if (ex.CurrentFrame >= explosionFrames.Count)
                    {
                        ex.Finished = true;
                        explosions.RemoveAt(i);
                        continue;
                    }

                    // Fade out in the last 40% of frames
                    if (ex.CurrentFrame > explosionFrames.Count * 0.6f)
                        ex.Alpha -= 0.15f;

                    // Fully faded — done
                    if (ex.Alpha <= 0f)
                    {
                        ex.Finished = true;
                        explosions.RemoveAt(i);
                        continue;
                    }
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
                gameTimer.Stop();

                MessageBox.Show($"Level {level} Complete!");

                level++;
                if (level > maxLevel)
                {
                    MessageBox.Show("You completed all levels!");
                    Close();
                    return;
                }

                bullets.Clear();
                explosions.Clear();
                smokeParticles.Clear();

                StartLevel(level);

                playerShootCooldown = 0;

                frameStopwatch.Restart();
                gameTimer.Start();
            }
        }
    }
}