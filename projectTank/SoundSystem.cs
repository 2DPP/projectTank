using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace projectTank
{
    public class SoundSystem
    {
        // Store loaded sounds as byte arrays (WAV)
        private Dictionary<string, byte[]> soundData = new Dictionary<string, byte[]>();

        // BGM
        private WaveOutEvent bgmOutput;
        private LoopStream bgmLoop;

        // Movement sounds
        private WaveOutEvent playerMoveOutput;
        private LoopStream playerMoveLoop;
        private WaveOutEvent aiMoveOutput;
        private LoopStream aiMoveLoop;

        private bool isPlayerMoving = false;
        private bool isAIMoving = false;

        /// <summary>
        /// Load a WAV sound from resource into memory
        /// </summary>
        public void LoadSound(string key, UnmanagedMemoryStream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                soundData[key] = ms.ToArray();
            }
        }

        /// <summary>
        /// Play a one-shot sound effect (shoot, explosion)
        /// </summary>
        public void Play(string key, float volume = 1.0f)
        {
            if (!soundData.ContainsKey(key)) return;

            var ms = new MemoryStream(soundData[key]);
            var reader = new WaveFileReader(ms);
            var output = new WaveOutEvent();
            output.Init(reader);
            output.Volume = volume;
            output.Play();

            // Dispose after playback
            output.PlaybackStopped += (s, e) =>
            {
                output.Dispose();
                reader.Dispose();
                ms.Dispose();
            };
        }

        /// <summary>
        /// Play looping BGM from resource
        /// </summary>
        public void PlayBGM(UnmanagedMemoryStream stream, float volume = 0.3f)
        {
            StopBGM();

            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;

            var reader = new Mp3FileReader(ms); // <-- Use Mp3FileReader for MP3
            bgmLoop = new LoopStream(reader);

            bgmOutput = new WaveOutEvent();
            bgmOutput.Init(bgmLoop);
            bgmOutput.Volume = volume;
            bgmOutput.Play();
        }

        public void StopBGM()
        {
            bgmOutput?.Stop();
            bgmOutput?.Dispose();
            bgmOutput = null;

            bgmLoop?.Dispose();
            bgmLoop = null;
        }

        /// <summary>
        /// Update looping player movement sound
        /// </summary>
        public void UpdatePlayerMovement(bool moving)
        {
            if (moving && !isPlayerMoving)
            {
                StartPlayerMoveLoop();
                isPlayerMoving = true;
            }
            else if (!moving && isPlayerMoving)
            {
                StopPlayerMoveLoop();
                isPlayerMoving = false;
            }
        }

        private void StartPlayerMoveLoop()
        {
            if (!soundData.ContainsKey("move")) return;

            var ms = new MemoryStream(soundData["move"]);
            var reader = new WaveFileReader(ms);
            playerMoveLoop = new LoopStream(reader);

            playerMoveOutput = new WaveOutEvent();
            playerMoveOutput.Init(playerMoveLoop);
            playerMoveOutput.Volume = 0.3f;
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

        /// <summary>
        /// Update looping AI movement sound
        /// </summary>
        public void UpdateAIMovement(bool moving)
        {
            if (moving && !isAIMoving)
            {
                StartAIMoveLoop();
                isAIMoving = true;
            }
            else if (!moving && isAIMoving)
            {
                StopAIMoveLoop();
                isAIMoving = false;
            }
        }

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