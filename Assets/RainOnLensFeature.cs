using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class RainOnLensFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Material rainMaterial;

        [Header("Fluid Dynamics")]
        [Tooltip("How many drops on screen.")]
        [Range(0f, 10f)] public float density = 0.5f;

        [Tooltip("Size of grid cells.")]
        [Range(0.01f, 15f)] public float dropSize = 1.0f;

        [Tooltip("Adds randomness to the path.")]
        [Range(0f, 10f)] public float meander = 0.5f;

        [Header("Velocity Physics")]
        [Tooltip("Vertical downward gravity.")]
        [Range(0f, 5f)] public float gravity = 1.0f;

        [Tooltip("Global time multiplier.")]
        [Range(0f, 5f)] public float timeScale = 1.0f;

        [Tooltip("RADIAL SPEED: Pushes drops outward and rotates them.")]
        [Range(0f, 10f)] public float speedEffect = 0.0f;

        [Header("Render Quality")]
        [Range(0f, 0.5f)] public float refraction = 0.05f;
        [Range(0f, 0.2f)] public float chromaticAberration = 0.02f;
        [Range(1f, 200f)] public float smoothness = 100.0f;
        [Range(0f, 3f)] public float brightness = 1.5f;
    }

    public Settings settings = new Settings();
    RainOnLensPass rainPass;

    public override void Create()
    {
        rainPass = new RainOnLensPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.rainMaterial == null) return;
        renderer.EnqueuePass(rainPass);
    }

    class RainOnLensPass : ScriptableRenderPass
    {
        private Settings settings;
        private Material material;

        private class PassData { public TextureHandle source; public Material material; }

        public RainOnLensPass(Settings settings)
        {
            this.settings = settings;
            this.material = settings.rainMaterial;
            this.renderPassEvent = settings.renderPassEvent;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (material == null || cameraData.cameraType == CameraType.Preview) return;
            if (!resourceData.activeColorTexture.IsValid()) return;

            TextureHandle source = resourceData.activeColorTexture;
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; desc.msaaSamples = 1;

            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_RainTemp", false);

            // Pass Data to Shader
            material.SetFloat("_Density", settings.density);
            material.SetFloat("_DropSize", settings.dropSize);
            material.SetFloat("_Wiggle", settings.meander);
            material.SetFloat("_Gravity", settings.gravity);
            material.SetFloat("_Speed", settings.timeScale);
            material.SetFloat("_WindStrength", settings.speedEffect);
            material.SetFloat("_Refraction", settings.refraction);
            material.SetFloat("_Blur", settings.chromaticAberration);
            material.SetFloat("_Specular", settings.smoothness);
            material.SetFloat("_Brightness", settings.brightness);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RainFX", out var passData))
            {
                passData.source = source;
                passData.material = material;
                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture("_BlitTexture", data.source);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RainCopy", out var passData))
            {
                passData.source = tempTexture;
                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }
        }
    }
}