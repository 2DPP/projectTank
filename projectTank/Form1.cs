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
        // =====================================================================
        // GAME OBJECTS
        // The main characters and elements that exist in the game world
        // =====================================================================
        private Tank player;                              // The tank controlled by the player
        private Tank enemy;                               // The tank controlled by the AI
        private TankAI enemyAI;                           // The AI brain that controls the enemy tank
        private PlayerController playerController;        // Handles player movement input
        private List<Bullet> bullets = new List<Bullet>();                   // All active bullets on screen
        private List<AnimatedGif> explosions = new List<AnimatedGif>();      // Active explosion animations
        private List<SmokeParticle> smokeParticles = new List<SmokeParticle>(); // Smoke trail behind bullets
        private Map map;                                  // The current level's map and walls
        private List<GifFrame> explosionFrames = new List<GifFrame>();       // Pre-loaded frames of the explosion GIF
        private Random rng = new Random();                // Shared random number generator

        // =====================================================================
        // INPUT FLAGS
        // These turn true/false when the player presses or releases movement keys
        // =====================================================================
        private bool up, down, left, right;

        // =====================================================================
        // SCALING FACTORS
        // The game was designed at 800x600 but runs fullscreen at any resolution.
        // These values stretch everything proportionally to fit the screen.
        // =====================================================================
        private const int ORIGINAL_WIDTH = 800;
        private const int ORIGINAL_HEIGHT = 600;
        private float scaleX, scaleY;           // How much wider/taller the screen is vs original
        private int scaledTankSize;             // Tank size adjusted for current resolution
        private int scaledBulletSize;           // Bullet size adjusted for current resolution
        private int scaledTankSpeed;            // Tank movement speed adjusted for current resolution
        private int scaledBulletSpeed;          // Bullet travel speed adjusted for current resolution

        // =====================================================================
        // GAME LOOP
        // The Timer fires every 16ms (~60 frames per second) to update and redraw
        // =====================================================================
        private Timer gameTimer = new Timer();
        private bool isTitleScreen = true;      // Tracks whether the title screen is showing

        // =====================================================================
        // TIMING
        // Measures real time between frames so animations stay smooth
        // even if the timer fires slightly late
        // =====================================================================
        private System.Diagnostics.Stopwatch frameStopwatch = new System.Diagnostics.Stopwatch();
        private int actualElapsed = 16;         // Real milliseconds since last frame

        // =====================================================================
        // SOUND SYSTEM
        // Manages all sound effects and background music
        // =====================================================================
        private SoundSystem soundSystem;

        // =====================================================================
        // SHOOTING COOLDOWN
        // Prevents the player from shooting too rapidly by enforcing a delay
        // =====================================================================
        private int playerShootCooldown = 0;
        private const int PLAYER_SHOOT_COOLDOWN_FRAMES = 20; // Frames to wait between shots

        // =====================================================================
        // PERFORMANCE CAP
        // Limits smoke particles to avoid lag when many bullets are on screen
        // =====================================================================
        private const int MAX_SMOKE_PARTICLES = 100;

        // =====================================================================
        // LEVEL SYSTEM
        // Tracks which level the player is on and how many levels exist
        // =====================================================================
        private int level = 1;
        private int maxLevel = 2;
        private bool levelCompleted = false;

        // =====================================================================
        // CONSTRUCTOR
        // Runs once when the program starts. Sets up the window and loads sounds.
        // =====================================================================
        public Form1()
        {
            InitializeComponent(); // Required WinForms setup

            // Make the game run fullscreen with no window border
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;

            // Allow the form to receive keyboard input directly
            this.KeyPreview = true;
            this.Focus();
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;

            // Prevents screen flicker during redraws
            this.DoubleBuffered = true;

            // Calculate how much to scale everything based on screen size vs original design size
            scaleX = (float)this.ClientSize.Width / ORIGINAL_WIDTH;
            scaleY = (float)this.ClientSize.Height / ORIGINAL_HEIGHT;
            float avgScale = (scaleX + scaleY) / 2f;

            scaledTankSize = (int)(50 * avgScale);
            scaledBulletSize = (int)(10 * avgScale);
            scaledTankSpeed = (int)(6 * avgScale);
            scaledBulletSpeed = (int)(15 * avgScale);

            // Load all sound effects and start background music
            soundSystem = new SoundSystem();
            soundSystem.LoadSound("move", Properties.Resources.TankMove);
            soundSystem.LoadSound("shoot", Properties.Resources.TankShoot);
            soundSystem.LoadSound("explosion", Properties.Resources.Explosion);
            soundSystem.PlayBGM(Properties.Resources.WarBGM);

            this.Load += Form1_Load;
        }

        // =====================================================================
        // FORM LOAD
        // Runs after the window appears. Loads assets and shows the title screen.
        // =====================================================================
        private void Form1_Load(object sender, EventArgs e)
        {
            // Pre-load all explosion GIF frames into memory so there's no lag mid-game
            LoadGifFrames(Properties.Resources.ExplosionFx);

            // Trigger the first draw — this will display the title screen
            Invalidate();
        }

        // =====================================================================
        // SETUP GAME
        // Initializes the game state for the current level
        // =====================================================================
        private void SetupGame()
        {
            StartLevel(level);      // Build the map, spawn tanks, wire up AI
            levelCompleted = false; // Reset completion flag for this level
        }

        // =====================================================================
        // GAME LOOP
        // Called every ~16ms by the Timer. Updates all game logic each frame.
        // =====================================================================
        private void GameLoop(object sender, EventArgs e)
        {
            // Measure how much real time has passed since the last frame
            actualElapsed = (int)frameStopwatch.ElapsedMilliseconds;
            if (actualElapsed < 1) actualElapsed = 1;   // Never let it be zero
            if (actualElapsed > 100) actualElapsed = 100; // Cap it to avoid huge jumps
            frameStopwatch.Restart();

            // Check if the player or AI is moving, then update their sounds
            bool playerIsMoving = up || down || left || right;
            bool aiIsMoving = enemy.IsMoving;
            soundSystem.UpdatePlayerMovement(playerIsMoving);
            soundSystem.UpdateAIMovement(aiIsMoving);

            // Update positions and logic for player and AI
            playerController.UpdateMovement(up, down, left, right, map);
            enemyAI.Update(map);

            // Update all game elements
            HandleBullets();     // Move bullets and check for collisions
            UpdateExplosions();  // Advance explosion animation frames
            CheckGameState();    // Check if anyone died or level is complete
            UpdateSmoke();       // Update smoke particle positions and lifetimes

            // Count down the player's shooting cooldown each frame
            if (playerShootCooldown > 0) playerShootCooldown--;

            Invalidate(); // Request a redraw of the screen
        }

        // =====================================================================
        // START LEVEL
        // Builds the map, spawns tanks, and sets up AI for the given level number
        // =====================================================================
        private void StartLevel(int lvl)
        {
            int screenWidth = ClientSize.Width;
            int screenHeight = ClientSize.Height;

            // Recalculate scale in case the window size changed
            scaleX = (float)screenWidth / ORIGINAL_WIDTH;
            scaleY = (float)screenHeight / ORIGINAL_HEIGHT;
            float scale = Math.Min(scaleX, scaleY); // Use the smaller scale to keep proportions

            scaledTankSize = (int)(50 * scale);
            scaledBulletSize = (int)(10 * scale);
            scaledTankSpeed = Math.Max(1, (int)(6 * scale));
            scaledBulletSpeed = Math.Max(1, (int)(15 * scale));

            // Create a new map sized to fill the screen
            map = new Map(screenWidth, screenHeight);

            // Add border walls so tanks and bullets can't leave the screen
            int borderThickness = Math.Max(10, (int)(screenHeight * 0.03f));
            map.AddWall(new Rectangle(0, 0, screenWidth, borderThickness));                              // Top
            map.AddWall(new Rectangle(0, screenHeight - borderThickness, screenWidth, borderThickness)); // Bottom
            map.AddWall(new Rectangle(0, 0, borderThickness, screenHeight));                             // Left
            map.AddWall(new Rectangle(screenWidth - borderThickness, 0, borderThickness, screenHeight)); // Right

            // Level 2 gets additional interior walls for more challenge
            if (lvl == 2)
            {
                map.AddWall(new Rectangle((int)(screenWidth * 0.25f), (int)(screenHeight * 0.1f),
                                          (int)(screenWidth * 0.05f), (int)(screenHeight * 0.3f)));
                map.AddWall(new Rectangle((int)(screenWidth * 0.5f), 0,
                                          (int)(screenWidth * 0.05f), (int)(screenHeight * 0.25f)));
                map.AddWall(new Rectangle((int)(screenWidth * 0.35f), (int)(screenHeight * 0.6f),
                                          (int)(screenWidth * 0.4f), (int)(screenHeight * 0.05f)));
            }

            // Spawn both tanks (position will be set safely in PlaceTanksSafely)
            player = new Tank(0, 0, Properties.Resources.TankA, scaledTankSize, scaledTankSize, scaledTankSpeed);
            enemy = new Tank(0, 0, Properties.Resources.TankB, scaledTankSize, scaledTankSize, scaledTankSpeed);

            // Place tanks at random positions that don't overlap walls
            PlaceTanksSafely();

            // Set up the player input handler and the AI controller
            playerController = new PlayerController(player, soundSystem);
            enemyAI = new TankAI(enemy, player, scaledBulletSize, scaledBulletSpeed, soundSystem);

            // When the AI fires a bullet, add it to the bullet list and play a sound
            enemyAI.OnShoot += (b) =>
            {
                if (b != null)
                {
                    bullets.Add(b);
                    soundSystem.Play("shoot", 0.7f);
                }
            };
        }

        // =====================================================================
        // LOAD GIF FRAMES
        // Breaks the explosion GIF into individual Bitmap frames so we can
        // control playback speed and fading manually during the game
        // =====================================================================
        void LoadGifFrames(Image gif)
        {
            var frames = new List<GifFrame>();

            // Get the number of frames in the GIF
            FrameDimension dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(dimension);

            // Try to read per-frame delay times stored in the GIF metadata
            PropertyItem delayItem = null;
            try { delayItem = gif.GetPropertyItem(0x5100); } catch { }

            for (int i = 0; i < frameCount; i++)
            {
                gif.SelectActiveFrame(dimension, i); // Jump to this frame in the GIF

                // Copy the frame into a fresh Bitmap so it's independent of the GIF stream
                Bitmap frame = new Bitmap(gif.Width, gif.Height, PixelFormat.Format32bppArgb);
                using (Graphics fg = Graphics.FromImage(frame))
                    fg.DrawImage(gif, 0, 0, gif.Width, gif.Height);

                // Read this frame's delay in milliseconds (GIF stores it in hundredths of a second)
                int delay = 100;
                if (delayItem != null && i * 4 + 3 < delayItem.Value.Length)
                {
                    delay = BitConverter.ToInt32(delayItem.Value, i * 4) * 10;
                    if (delay <= 0) delay = 100; // Fall back to 100ms if the value is invalid
                }

                frames.Add(new GifFrame { Image = frame, Delay = delay });
            }

            explosionFrames = frames; // Store all frames ready for use during gameplay
        }

        // =====================================================================
        // PLACE TANKS SAFELY
        // Randomly positions each tank, retrying until they don't overlap any wall
        // =====================================================================
        private void PlaceTanksSafely()
        {
            int attempts;

            // Try up to 100 random positions for the player tank
            attempts = 0;
            do
            {
                int x = rng.Next(scaledTankSize, ClientSize.Width - scaledTankSize);
                int y = rng.Next(scaledTankSize, ClientSize.Height - scaledTankSize);
                player.Bounds = new Rectangle(x, y, scaledTankSize, scaledTankSize);
                attempts++;
            } while (map.IsColliding(player.Bounds) && attempts < 100);

            // Try up to 100 random positions for the enemy tank
            attempts = 0;
            do
            {
                int x = rng.Next(scaledTankSize, ClientSize.Width - scaledTankSize);
                int y = rng.Next(scaledTankSize, ClientSize.Height - scaledTankSize);
                enemy.Bounds = new Rectangle(x, y, scaledTankSize, scaledTankSize);
                attempts++;
            } while (map.IsColliding(enemy.Bounds) && attempts < 100);
        }

        // =====================================================================
        // KEY DOWN
        // Called when any key is pressed. Handles both title screen and in-game input.
        // =====================================================================
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // On the title screen, only SPACE starts the game
            if (isTitleScreen)
            {
                if (e.KeyCode == Keys.Space)
                {
                    isTitleScreen = false;

                    SetupGame(); // Build the first level

                    // Wire up and start the game loop timer
                    gameTimer.Interval = 16;
                    gameTimer.Tick += GameLoop;
                    frameStopwatch.Restart();
                    gameTimer.Start();
                }
                return; // Ignore all other keys while on title screen
            }

            // In-game key handling
            switch (e.KeyCode)
            {
                case Keys.W: up = true; break; // Move up
                case Keys.S: down = true; break; // Move down
                case Keys.A: left = true; break; // Move left
                case Keys.D: right = true; break; // Move right

                case Keys.Space:
                    // Fire a bullet if the cooldown has expired
                    if (playerShootCooldown <= 0)
                    {
                        bullets.Add(new Bullet(player, scaledBulletSize, scaledBulletSize, scaledBulletSpeed));
                        soundSystem.Play("shoot", 0.7f);
                        playerShootCooldown = PLAYER_SHOOT_COOLDOWN_FRAMES; // Reset cooldown
                    }
                    break;

                case Keys.Escape: Close(); break; // Quit the game
            }
        }

        // =====================================================================
        // KEY UP
        // Called when a movement key is released — stops the tank from moving
        // =====================================================================
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

        // =====================================================================
        // SMOKE PARTICLE (Nested Class)
        // Represents a single puff of smoke left behind by a moving bullet
        // =====================================================================
        class SmokeParticle
        {
            public float X, Y;      // Current position on screen
            public float Size;      // Current diameter of the smoke puff
            public float Alpha;     // Opacity (1 = fully visible, 0 = invisible)
            public float Life;      // How long this particle has existed (ms)
            public float MaxLife;   // How long it will live before disappearing
            public float VelX, VelY; // Drift velocity each frame

            public SmokeParticle(float x, float y, Random rng)
            {
                X = x;
                Y = y;
                Size = rng.Next(4, 8);   // Start as a small random-sized circle
                Alpha = 1f;               // Start fully visible
                Life = 0f;
                MaxLife = rng.Next(300, 500); // Live between 300 and 500 milliseconds
                VelX = (float)(rng.NextDouble() - 0.5) * 0.5f;        // Slight random horizontal drift
                VelY = (float)(-0.3 - rng.NextDouble() * 0.5);        // Drifts upward
            }
        }

        // =====================================================================
        // UPDATE SMOKE
        // Moves and ages all smoke particles each frame, removing expired ones
        // =====================================================================
        private void UpdateSmoke()
        {
            // Iterate backwards so we can safely remove items while looping
            for (int i = smokeParticles.Count - 1; i >= 0; i--)
            {
                var p = smokeParticles[i];

                p.Life += actualElapsed; // Age the particle by real elapsed time

                float lifeRatio = p.Life / p.MaxLife;
                p.Alpha = 1f - lifeRatio; // Fade out as it ages
                p.Size += 0.3f;           // Expand slightly each frame
                p.X += p.VelX;         // Drift horizontally
                p.Y += p.VelY;         // Drift upward

                // Remove the particle once its lifetime is over
                if (p.Life >= p.MaxLife)
                    smokeParticles.RemoveAt(i);
            }
        }

        // =====================================================================
        // GIF FRAME (Nested Class)
        // Stores one frame of the explosion animation with its display duration
        // =====================================================================
        class GifFrame
        {
            public Image Image; // The bitmap for this frame
            public int Delay;   // How long to show this frame in milliseconds
        }

        // =====================================================================
        // ANIMATED GIF (Nested Class)
        // Tracks playback state for one explosion animation instance on screen
        // =====================================================================
        class AnimatedGif
        {
            public Rectangle Bounds;        // Where on screen to draw the explosion
            public int CurrentFrame = 0;    // Which frame is currently showing
            public int Timer = 0;           // Time accumulated on the current frame
            public float Alpha = 1f;        // Opacity used for the fade-out effect
            public int TotalElapsed = 0;    // Total time this explosion has been alive
            public bool Finished = false;   // Whether this explosion should be removed
            public const int MAX_LIFETIME = 1200; // Hard cap: remove after 1.2 seconds no matter what

            public AnimatedGif(Rectangle bounds)
            {
                Bounds = bounds;
            }
        }

        // =====================================================================
        // ON PAINT
        // Called every frame to draw everything to the screen
        // =====================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // If the title screen is active, draw it and stop here
            if (isTitleScreen)
            {
                g.DrawImage(Properties.Resources.Title_screen,
                    new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                base.OnPaint(e);
                return;
            }

            // Draw all map walls (borders and level obstacles)
            if (map != null && map.Walls != null)
            {
                foreach (var wall in map.Walls)
                {
                    if (wall != null)
                        wall.Draw(g);
                }
            }

            // Draw the player and enemy tanks
            if (player != null && player.TankImage != null)
                DrawTank(g, player);
            if (enemy != null && enemy.TankImage != null)
                DrawTank(g, enemy);

            // Draw all active bullets as yellow circles
            foreach (var b in bullets.ToList())
            {
                if (b != null)
                    g.FillEllipse(Brushes.Yellow, b.Bounds);
            }

            // Draw all active smoke particles with their current alpha
            foreach (var p in smokeParticles.ToList())
            {
                int alpha = (int)(p.Alpha * 255);
                if (alpha <= 0) continue;   // Skip invisible particles
                if (alpha > 255) alpha = 255;

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 100, 100, 100)))
                {
                    g.FillEllipse(brush, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
                }
            }

            // Draw all active explosion animations
            foreach (var ex in explosions.ToList())
            {
                if (ex.Finished) continue; // Skip completed explosions
                if (ex.CurrentFrame < 0 || ex.CurrentFrame >= explosionFrames.Count) continue;

                var frame = explosionFrames[ex.CurrentFrame];
                float alpha = Math.Max(0f, Math.Min(1f, ex.Alpha)); // Clamp alpha to [0, 1]

                if (alpha <= 0f) continue;

                if (alpha >= 1f)
                {
                    // Full opacity — draw directly (faster, no color matrix needed)
                    g.DrawImage(frame.Image, ex.Bounds);
                }
                else
                {
                    // Fading out — use a ColorMatrix to apply transparency
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = alpha; // Matrix33 controls the alpha channel
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

            // Draw health bars above each tank
            if (player != null) DrawHealthBar(g, player);
            if (enemy != null) DrawHealthBar(g, enemy);

            base.OnPaint(e);
        }

        // =====================================================================
        // DRAW TANK
        // Draws a tank rotated to face its current direction
        // =====================================================================
        private void DrawTank(Graphics g, Tank tank)
        {
            // Convert the tank's direction enum into a rotation angle in degrees
            float angle = 0;
            switch (tank.Direction)
            {
                case Tank.TankDirection.Up: angle = 0; break;
                case Tank.TankDirection.Right: angle = 90; break;
                case Tank.TankDirection.Down: angle = 180; break;
                case Tank.TankDirection.Left: angle = 270; break;
            }

            // Translate the origin to the tank's center, rotate, then draw
            g.TranslateTransform(tank.Bounds.X + tank.Bounds.Width / 2,
                                 tank.Bounds.Y + tank.Bounds.Height / 2);
            g.RotateTransform(angle);
            g.DrawImage(tank.TankImage,
                        -tank.Bounds.Width / 2,
                        -tank.Bounds.Height / 2,
                         tank.Bounds.Width,
                         tank.Bounds.Height);
            g.ResetTransform(); // Always reset after drawing so other elements aren't affected
        }

        // =====================================================================
        // DRAW HEALTH BAR
        // Draws a red background bar and a green foreground bar above the tank
        // =====================================================================
        private void DrawHealthBar(Graphics g, Tank tank)
        {
            int barWidth = tank.Bounds.Width;
            int barHeight = 6;
            int healthWidth = (int)(barWidth * (tank.Health / 100.0)); // Shrinks as health drops

            g.FillRectangle(Brushes.DarkRed, tank.Bounds.X, tank.Bounds.Y - 10, barWidth, barHeight); // Background
            g.FillRectangle(Brushes.LimeGreen, tank.Bounds.X, tank.Bounds.Y - 10, healthWidth, barHeight); // Foreground
        }

        // =====================================================================
        // HANDLE BULLETS
        // Moves every bullet and checks for collisions with tanks and walls
        // =====================================================================
        private void HandleBullets()
        {
            // Iterate backwards so removing an item doesn't skip the next one
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                bullet.Move(); // Advance the bullet along its trajectory

                // Spawn a smoke puff at the bullet's position (only if under the cap)
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

                // Remove bullet if it has left the visible screen area
                if (bullet.Bounds.X < 0 || bullet.Bounds.Y < 0 ||
                    bullet.Bounds.X > ClientSize.Width || bullet.Bounds.Y > ClientSize.Height)
                {
                    bullets.RemoveAt(i);
                    continue;
                }

                // Player's bullet hit the enemy
                if (bullet.Owner == player && bullet.Bounds.IntersectsWith(enemy.Bounds))
                {
                    enemy.TakeDamage(10);
                    CreateExplosion(enemy.Bounds);
                    bullets.RemoveAt(i);
                    soundSystem.Play("explosion", 0.7f);
                    continue;
                }

                // Enemy's bullet hit the player
                if (bullet.Owner == enemy && bullet.Bounds.IntersectsWith(player.Bounds))
                {
                    player.TakeDamage(10);
                    CreateExplosion(player.Bounds);
                    bullets.RemoveAt(i);
                    soundSystem.Play("explosion", 0.7f);
                    continue;
                }

                // Bullet hit a wall
                if (map.IsColliding(bullet.Bounds))
                {
                    CreateExplosion(bullet.Bounds);
                    bullets.RemoveAt(i);
                    soundSystem.Play("explosion", 0.7f);
                }
            }
        }

        // =====================================================================
        // CREATE EXPLOSION
        // Spawns a new explosion animation centered on the given bounds
        // =====================================================================
        private void CreateExplosion(Rectangle bounds)
        {
            // Scale the explosion size relative to whatever was hit
            int baseSize = Math.Max(bounds.Width, bounds.Height);
            int size = Math.Max(32, (int)(baseSize * 1.2)); // Always at least 32px

            // Center the explosion over the hit area
            Rectangle drawBounds = new Rectangle(
                bounds.X + (bounds.Width - size) / 2,
                bounds.Y + (bounds.Height - size) / 2,
                size,
                size
            );

            explosions.Add(new AnimatedGif(drawBounds));
        }

        // =====================================================================
        // UPDATE EXPLOSIONS
        // Advances each explosion animation frame by frame and removes finished ones
        // =====================================================================
        private void UpdateExplosions()
        {
            for (int i = explosions.Count - 1; i >= 0; i--)
            {
                var ex = explosions[i];
                ex.Timer += actualElapsed; // Time on the current frame
                ex.TotalElapsed += actualElapsed; // Total time alive

                // Hard timeout — remove the explosion even if something went wrong
                if (ex.TotalElapsed >= AnimatedGif.MAX_LIFETIME)
                {
                    explosions.RemoveAt(i);
                    continue;
                }

                // Remove if already marked as done
                if (ex.Finished)
                {
                    explosions.RemoveAt(i);
                    continue;
                }

                // Safety check — frame index should never exceed the frame list
                if (ex.CurrentFrame >= explosionFrames.Count)
                {
                    ex.Finished = true;
                    explosions.RemoveAt(i);
                    continue;
                }

                // Check if enough time has passed to advance to the next frame
                if (ex.Timer >= explosionFrames[ex.CurrentFrame].Delay)
                {
                    ex.Timer = 0;
                    ex.CurrentFrame++;

                    // We've shown all frames — mark it done
                    if (ex.CurrentFrame >= explosionFrames.Count)
                    {
                        ex.Finished = true;
                        explosions.RemoveAt(i);
                        continue;
                    }

                    // Start fading out during the last 40% of frames
                    if (ex.CurrentFrame > explosionFrames.Count * 0.6f)
                        ex.Alpha -= 0.15f;

                    // Fully transparent — no point drawing it anymore
                    if (ex.Alpha <= 0f)
                    {
                        ex.Finished = true;
                        explosions.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        // =====================================================================
        // CHECK GAME STATE
        // Called every frame to detect win/lose conditions and handle level transitions
        // =====================================================================
        private void CheckGameState()
        {
            // Player died — game over
            if (!player.IsAlive)
            {
                gameTimer.Stop();
                MessageBox.Show("You Lose!");
                Close();
                return;
            }

            // Enemy died — level complete
            if (!enemy.IsAlive)
            {
                gameTimer.Stop();
                MessageBox.Show($"Level {level} Complete!");

                level++; // Advance to the next level

                // All levels finished — player wins
                if (level > maxLevel)
                {
                    MessageBox.Show("You completed all levels!");
                    Close();
                    return;
                }

                // Clean up all active objects before loading the next level
                bullets.Clear();
                explosions.Clear();
                smokeParticles.Clear();

                StartLevel(level); // Load the new level

                playerShootCooldown = 0; // Reset shooting cooldown

                frameStopwatch.Restart();
                gameTimer.Start(); // Resume the game loop
            }
        }
    }
}