using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using POpusCodec;

namespace Restrainite.Patches;

[HarmonyPatch]
internal static class VoiceQuality
{
    // Opus/Resonite clamps the stream bitrate to this floor (see OpusStream.RefreshResources).
    private const int MinBitrate = 2400;
    private const int MaxBitrate = 500000;

    // RefreshResources runs at the top of every EncodeSamples and only builds the encoder for the
    // local user's own outgoing voice stream. We re-assert the encoder bitrate here rather than
    // touching the BitRate Sync field, because this can run on the background encode thread and a
    // direct opus_encoder_ctl call is thread-local, cheap, and needs no synced state changes.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OpusStream<MonoSample>), "RefreshResources")]
    private static void OpusStream_RefreshResources_Postfix(OpusStream<MonoSample> __instance, OpusEncoder? ___encoder)
    {
        // Remote streams decode instead of encode, so they have no encoder to touch.
        if (___encoder == null || !__instance.IsOwnedByLocalUser) return;

        var baseBitrate = MathX.Clamp(__instance.BitRate.Value, MinBitrate, MaxBitrate);
        var desired = baseBitrate;

        if (Restrictions.VoiceQuality.IsRestricted)
        {
            var quality = Restrictions.VoiceQuality.LowestFloat.Value;
            if (!float.IsNaN(quality))
                desired = (int)MathX.Clamp(MathX.Lerp(MinBitrate, baseBitrate, quality), MinBitrate, baseBitrate);
        }

        // Only issue the native call when the target actually changes; restores automatically when
        // the restriction is lifted (desired falls back to the stream's configured bitrate).
        if (___encoder.Bitrate != desired) ___encoder.Bitrate = desired;
    }
}
