using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GlitchPostProcessing : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material blitMaterial;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    class GlitchPass : ScriptableRenderPass
    {
        private readonly Material blitMaterial;
        private readonly string profilerTag = "Glitch PostProcess";

        private readonly int tempRTId = Shader.PropertyToID("_TempColorTex");
        private RenderTargetIdentifier tempRT;

        public GlitchPass(Material mat, RenderPassEvent passEvent)
        {
            blitMaterial = mat;
            renderPassEvent = passEvent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (blitMaterial == null) return;

            // Create a temporary color texture matching the camera target.
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            cmd.GetTemporaryRT(tempRTId, desc, FilterMode.Bilinear);
            tempRT = new RenderTargetIdentifier(tempRTId);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (blitMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            // URP 14 camera color target.
            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Copy current screen color to temp.
            Blit(cmd, source, tempRT);

            // Apply glitch material back to camera color.
            Blit(cmd, tempRT, source, blitMaterial);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) return;

            // Release temporary texture after this camera finishes rendering.
            cmd.ReleaseTemporaryRT(tempRTId);
        }
    }

    private GlitchPass glitchPass;

    public override void Create()
    {
        glitchPass = new GlitchPass(settings.blitMaterial, settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blitMaterial == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        renderer.EnqueuePass(glitchPass);
    }
}