using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Microsoft.VisualBasic.Logging;
using RE.Core.Audio;
using RE.Core.Scripting.Attributes;
using Log = Serilog.Log;

namespace RE.Core.World.Components
{
    public class AudioSourceComponent : Component
    {
        [EditorProperty] public string AudioEvent { get; set; }

        public override void Start()
        {
            if (!SoundManager.Exists(AudioEvent))
            {
                Log.Error("Audio event not found: {AudioPath}", AudioEvent);
                return;
            }
            SoundManager.PlayOneShotEvent(AudioEvent);
        } 

        public override void OnDestroy()
        {
            SoundManager.StopAll();
        }

        public override JsonNode GetSaveData()
        {
            return this.GetDataForProperties();
        }
    }
}
