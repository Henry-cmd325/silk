#:package Silk.NET.Windowing@2.21.0
#:package Silk.NET.Maths@2.21.0
#:package Veldrid@4.9.0
#:package Veldrid.Sdl2@4.9.0
#:package Veldrid.StartupUtilities@4.9.0
#:package Veldrid.SPIRV@1.0.15

using System;
using System.Numerics;
using System.Text;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Veldrid.SPIRV;

// --- 1. Declaración de variables (sin private static) ---
IWindow _window = null!;
GraphicsDevice _gd = null!;
CommandList _cl = null!;
DeviceBuffer _vertexBuffer = null!;
Pipeline _pipeline = null!;
Shader[] _shaders = null!;

// --- 2. Constantes ---
const string VertexCode = @"
    #version 450
    layout(location = 0) in vec2 Position;
    layout(location = 1) in vec4 Color;
    layout(location = 0) out vec4 fsin_Color;
    void main()
    {
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
    }";

const string FragmentCode = @"
    #version 450
    layout(location = 0) in vec4 fsin_Color;
    layout(location = 0) out vec4 fsout_Color;
    void main()
    {
    fsout_Color = fsin_Color;
    }";

// --- 3. Lógica Principal ---
WindowOptions options = WindowOptions.Default;
options.Size = new Vector2D<int>(800, 600);
options.Title = "Hola Veldrid - Primer Triángulo";

_window = Window.Create(options);
_window.Load += OnLoad;
_window.Render += OnRender;
_window.Run();

SwapchainSource GetSwapchainSource(IWindow window)
{
    // Si estamos en Windows
    if (window.Native.Win32.HasValue)
    {
        var handle = window.Native.Win32.Value.Hwnd;
        var hinstance = window.Native.Win32.Value.Hinstance;
        return SwapchainSource.CreateWin32(handle, hinstance);
    }
    // Si estuviéramos en Linux (X11) o Mac, aquí irían los 'else if'
    // Para este ejemplo asumiremos Windows o tiramos error
    throw new PlatformNotSupportedException("Por ahora este ejemplo solo soporta Windows.");
}

// --- 4. Funciones Locales (pueden acceder a las variables de arriba) ---
void OnLoad()
{
    // 1. Crear dispositivo gráfico
    _gd = GetSwapchainSource(_window).CreateGraphicsDevice();
    var factory = _gd.ResourceFactory;

    // 2. Crear datos de vértices (Un triángulo)
    VertexPositionColor[] vertices =
    {
        new VertexPositionColor(new Vector2(0.0f, 0.5f), RgbaFloat.Red),    // Arriba
        new VertexPositionColor(new Vector2(0.5f, -0.5f), RgbaFloat.Green), // Derecha Abajo
        new VertexPositionColor(new Vector2(-0.5f, -0.5f), RgbaFloat.Blue)  // Izquierda Abajo
    };

    // 3. Crear y llenar el Buffer en GPU
    BufferDescription vbDescription = new BufferDescription(
        (uint)(24 * vertices.Length),
        BufferUsage.VertexBuffer);
    _vertexBuffer = factory.CreateBuffer(vbDescription);
    _gd.UpdateBuffer(_vertexBuffer, 0, vertices);

    // 4. Compilar Shaders (Texto -> SPIR-V -> Backend Nativo)
    ShaderDescription vertexDesc = new ShaderDescription(
        ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main");
    ShaderDescription fragmentDesc = new ShaderDescription(
        ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main");
    
    _shaders = factory.CreateFromSpirv(vertexDesc, fragmentDesc);

    // 5. Definir el Layout (El mapa de los datos)
    VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
        new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
    );

    // 6. Crear el Pipeline (La configuración de dibujo)
    GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
    pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
    pipelineDescription.DepthStencilState = DepthStencilStateDescription.Disabled;
    pipelineDescription.RasterizerState = RasterizerStateDescription.Default;
    pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleList;
    pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>(); // No usamos recursos externos aun
    pipelineDescription.ShaderSet = new ShaderSetDescription(
        new[] { vertexLayout },
        _shaders);
    pipelineDescription.Outputs = _gd.SwapchainFramebuffer.OutputDescription;

    _pipeline = factory.CreateGraphicsPipeline(pipelineDescription);

    // 7. Crear la lista de comandos
    _cl = factory.CreateCommandList();
}

void OnRender(double delta)
{
    // --- GRABACIÓN DE COMANDOS ---
    _cl.Begin();
    
    // Decirle dónde dibujar (en la ventana)
    _cl.SetFramebuffer(_gd.SwapchainFramebuffer);
    
    // Limpiar la pantalla (Color Gris Oscuro de fondo)
    _cl.ClearColorTarget(0, new RgbaFloat(0.1f, 0.1f, 0.1f, 1f));
    
    // Cargar nuestra "Máquina" (Pipeline)
    _cl.SetPipeline(_pipeline);
    
    // Cargar nuestros datos (Buffer)
    _cl.SetVertexBuffer(0, _vertexBuffer);
    
    // DIBUJAR: 3 vértices, 1 instancia, empezar en 0, instancia 0
    _cl.Draw(3, 1, 0, 0);
    
    _cl.End();
    // -----------------------------

    // Enviar a la GPU
    _gd.SubmitCommands(_cl);
    
    // Mostrar en pantalla
    _gd.SwapBuffers();
}

// --- 5. Definiciones de Tipos (SIEMPRE al final del archivo) ---
struct VertexPositionColor
{
    public Vector2 Position; // 8 bytes
    public RgbaFloat Color;  // 16 bytes

    public VertexPositionColor(Vector2 position, RgbaFloat color)
    {
        Position = position;
        Color = color;
    }
}