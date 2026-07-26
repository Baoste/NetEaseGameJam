using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelPostProcessing : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material blitMaterial;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    class CustomPostProcessPass : ScriptableRenderPass
    {
        private Material blitMaterial;
        private string profilerTag = "Custom PostProcess Pass";

        private int tempRTId = Shader.PropertyToID("_TempColorTex");
        private RenderTargetIdentifier tempRTIdentifier;

        public CustomPostProcessPass(Material mat, RenderPassEvent passEvent)
        {
            this.blitMaterial = mat;
            this.renderPassEvent = passEvent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (blitMaterial == null) return;

            var cameraDesc = renderingData.cameraData.cameraTargetDescriptor;
            cameraDesc.depthBufferBits = 0;

            cmd.GetTemporaryRT(tempRTId, cameraDesc, FilterMode.Bilinear);
            tempRTIdentifier = new RenderTargetIdentifier(tempRTId);

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (blitMaterial == null) return;

            var cmd = CommandBufferPool.Get(profilerTag);

            var sourceHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var source = sourceHandle.rt;

            cmd.Blit(source, tempRTIdentifier);
            cmd.Blit(tempRTIdentifier, source, blitMaterial);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // �ͷ���ʱRT
            if (blitMaterial != null)
            {
                cmd.ReleaseTemporaryRT(tempRTId);
            }
        }
    }

    CustomPostProcessPass m_Pass;

    public override void Create()
    {
        m_Pass = new CustomPostProcessPass(settings.blitMaterial, settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blitMaterial == null)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        renderer.EnqueuePass(m_Pass);
    }
}
