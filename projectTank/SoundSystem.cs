using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

// SoundSystem.cs
// Manages all audio in the game using NAudio. Handles one-shot sound effects
// (shoot, explosion), a looping background music track, and separate looping
// engine sounds for the player and AI tanks that start/stop based on movement.

namespace projectTank
{
    // =========================================================================
    // SOUND SYSTEM
    // Manages all audio in the game: background music, sound effects,
    // and looping movement sounds for both the player and the AI tank.
    // Uses the NAudio library to play WAV and MP3 files from embedded resources.
    // =========================================================================
    public class SoundSystem
    {
        // =====================================================================
        // SOUND STORAGE
        // All loaded sounds are kept in memory as raw byte arrays.
        // Using a Dictionary lets us look up any sound by a simple name key
        // (e.g. "shoot", "move", "explosion").
        // =====================================================================
        private Dictionary<string, byte[]> soundData = new Dictionary<string, byte[]>();

        // =====================================================================
        // BACKGROUND MUSIC
        // BGM plays on a continuous loop using a dedicated audio output.
        // WaveOutEvent is NAudio's audio playback device.
        // LoopStream wraps the audio reader to make it repeat automatically.
        // =====================================================================
        private WaveOutEvent bgmOutput;
        private LoopStream bgmLoop;

        // =====================================================================
        // MOVEMENT SOUNDS
        // Separate looping audio channels for the player tank and the AI tank.
        // Each has its own output and loop stream so they can start and stop
        // independently without affecting each other.
        // =====================================================================
        private WaveOutEvent playerMoveOutput;
        private LoopStream playerMoveLoop;
        private WaveOutEvent aiMoveOutput;
        private LoopStream aiMoveLoop;

        // Tracks whether each tank is currently moving
        // so we don't restart the sound every single frame
        private bool isPlayerMoving = false;
        private bool isAIMoving = false;

        // =====================================================================
        // LOAD SOUND
        // Reads a WAV audio resource into a byte array and stores it by key.
        // Loading into memory upfront means no file I/O delay during gameplay.
        // =====================================================================
        public void LoadSound(string key, UnmanagedMemoryStream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                soundData[key] = ms.ToArray(); // Store the raw bytes
            }
        }

        // =====================================================================
        // PLAY (One-Shot Sound Effect)
        // Creates a temporary audio output, plays the sound once, then
        // automatically disposes of all resources when playback finishes.
        // Used for short effects like shooting and explosions.
        // =====================================================================
        public void Play(string key, float volume = 1.0f)
        {
            if (!soundData.ContainsKey(key)) return; // Silently skip unknown sounds

            // Wrap the stored bytes in a stream so NAudio can read them
            var ms = new MemoryStream(soundData[key]);
            var reader = new WaveFileReader(ms);
            var output = new WaveOutEvent();

            output.Init(reader);
            output.Volume = volume;
            output.Play();

            // Clean up all resources automatically once the sound finishes
            output.PlaybackStopped += (s, e) =>
            {
                output.Dispose();
                reader.Dispose();
                ms.Dispose();
            };
        }

        // =====================================================================
        // PLAY BGM (Background Music Loop)
        // Stops any existing BGM, then starts the new track on a continuous loop.
        // Uses Mp3FileReader since the BGM is stored as an MP3 resource.
        // =====================================================================
        public void PlayBGM(UnmanagedMemoryStream stream, float volume = 0.3f)
        {
            StopBGM(); // Stop any currently playing BGM before starting a new one

            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0; // Rewind to the start before reading

            var reader = new Mp3FileReader(ms); // MP3-specific reader from NAudio
            bgmLoop = new LoopStream(reader);   // Wrap it so it repeats forever

            bgmOutput = new WaveOutEvent();
            bgmOutput.Init(bgmLoop);
            bgmOutput.Volume = volume;
            bgmOutput.Play();
        }

        // =====================================================================
        // STOP BGM
        // Stops and releases all BGM audio resources.
        // The ?. operator safely skips the call if the object is already null.
        // =====================================================================
        public void StopBGM()
        {
            bgmOutput?.Stop();
            bgmOutput?.Dispose();
            bgmOutput = null;

            bgmLoop?.Dispose();
            bgmLoop = null;
        }

        // =====================================================================
        // UPDATE PLAYER MOVEMENT SOUND
        // Called every frame with whether the player is currently moving.
        // Starts the loop when movement begins, stops it when movement ends.
        // The state flags prevent restarting the sound on every single frame.
        // =====================================================================
        public void UpdatePlayerMovement(bool moving)
        {
            if (moving && !isPlayerMoving)
            {
                StartPlayerMoveLoop(); // Player just started moving
                isPlayerMoving = true;
            }
            else if (!moving && isPlayerMoving)
            {
                StopPlayerMoveLoop();  // Player just stopped moving
                isPlayerMoving = false;
            }
        }

        // =====================================================================
        // START / STOP PLAYER MOVE LOOP
        // Creates a fresh looping audio output for the player movement sound.
        // Stop cleans up the output and stream to free memory.
        // =====================================================================
        private void StartPlayerMoveLoop()
        {
            if (!soundData.ContainsKey("move")) return;

            var ms = new MemoryStream(soundData["move"]);
            var reader = new WaveFileReader(ms);
            playerMoveLoop = new LoopStream(reader);

            playerMoveOutput = new WaveOutEvent();
            playerMoveOutput.Init(playerMoveLoop);
            playerMoveOutput.Volume = 0.3f; // Quieter than effects so it doesn't overpower
            playerMoveOutput.Play();
        }

        private void StopPlayerMoveLoop()
        {
            playerMoveOutput?.Stop();
            playerMoveOutput?.Dispose();
            playerMoveOutput = null;

            playerMoveLoop?.Dispose();
            playerMoveLoop = null;
        }

        // =====================================================================
        // UPDATE AI MOVEMENT SOUND
        // Same logic as the player movement sound but for the AI tank.
        // Having separate channels means both tanks can move and sound
        // at the same time without interfering with each other.
        // =====================================================================
        public void UpdateAIMovement(bool moving)
        {
            if (moving && !isAIMoving)
            {
                StartAIMoveLoop(); // AI just started moving
                isAIMoving = true;
            }
            else if (!moving && isAIMoving)
            {
                StopAIMoveLoop();  // AI just stopped moving
                isAIMoving = false;
            }
        }

        // =====================================================================
        // START / STOP AI MOVE LOOP
        // Identical structure to the player move loop — separate instance
        // so the AI sound doesn't share state with the player sound.
        // =====================================================================
        private void StartAIMoveLoop()
        {
            if (!soundData.ContainsKey("move")) return;

            var ms = new MemoryStream(soundData["move"]);
            var reader = new WaveFileReader(ms);
            aiMoveLoop = new LoopStream(reader);

            aiMoveOutput = new WaveOutEvent();
            aiMoveOutput.Init(aiMoveLoop);
            aiMoveOutput.Volume = 0.3f;
            aiMoveOutput.Play();
        }

        private void StopAIMoveLoop()
        {
            aiMoveOutput?.Stop();
            aiMoveOutput?.Dispose();
            aiMoveOutput = null;

            aiMoveLoop?.Dispose();
            aiMoveLoop = null;
        }
    }
}