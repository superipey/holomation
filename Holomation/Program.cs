using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using Urho;
using Urho.Actions;
using Urho.SharpReality;
using Urho.Shapes;
using Urho.Resources;
using RabbitMQ.Client;
using System.Diagnostics;
using Urho.Gui;
using Windows.Media.MediaProperties;
using System.IO;
using Windows.Storage.Streams;
using Urho.Urho2D;
using Windows.Media.Capture;
using System.Threading.Tasks;
using CognitiveServices;
using Windows.Storage;
using Windows.Media.Effects;
using Windows.Foundation.Collections;
using Newtonsoft.Json;

namespace Holo1
{
    internal class Program
    {
        [MTAThread]
        static void Main()
        {
            var appViewSource = new UrhoAppViewSource<HelloWorldApplication>(new ApplicationOptions("Data"));
            //appViewSource.UrhoAppViewCreated += OnViewCreated;
            CoreApplication.Run(appViewSource);
        }

        static void OnViewCreated(UrhoAppView view)
        {
            view.WindowIsSet += View_WindowIsSet;
        }

        static void View_WindowIsSet(Windows.UI.Core.CoreWindow coreWindow)
        {
            // you can subscribe to CoreWindow events here
        }
    }

    public class HelloWorldApplication : StereoApplication
    {


        public class Response
        {
            public string status { get; set; }
            public Recognitionresult recognitionResult { get; set; }
        }

        public class Recognitionresult
        {
            public Line[] lines { get; set; }
        }

        public class Line
        {
            public int[] boundingBox { get; set; }
            public string text { get; set; }
            public Word[] words { get; set; }
        }

        public class Word
        {
            public int[] boundingBox { get; set; }
            public string text { get; set; }
        }


        Node buttonLampu;
        Node buttonKipas;

        RMQ rmq;

        CSHttpClientSample.CognitiveTool cTool;

        MediaCapture mediaCapture;
        Node busyIndicatorNode;

        bool busy;

        //Material Earth;

        //UIElement ui_node;

        //readonly Color validPositionColor = Color.Gray;
        //readonly Color invalidPositionColor = Color.Red;

        public HelloWorldApplication(ApplicationOptions opts) : base(opts) { }


        protected override async void Start()
        {
            ResourceCache.AutoReloadResources = true;
            base.Start();

            // Busy Indicator
            busyIndicatorNode = Scene.CreateChild();
            busyIndicatorNode.SetScale(0.06f);
            busyIndicatorNode.CreateComponent<BusyIndicator>();

            rmq = new RMQ();

            rmq.InitRMQConnection(); // inisialisasi parameter (secara default) untuk koneksi ke server RMQ
            rmq.CreateRMQConnection(); // memulai koneksi dengan RMQ
            rmq.CreateRMQChannel();

            //rmq.Disconnect();

            // Enable input
            EnableGestureManipulation = true;
            EnableGestureTapped = true;

            // Create a node for the Earth
            buttonLampu = Scene.CreateChild();
            buttonLampu.Position = new Vector3(-0.5f, 0, 1.5f); //1.5m away
            buttonLampu.SetScale(0.3f); //D=30cm

            buttonKipas = Scene.CreateChild();
            buttonKipas.Position = new Vector3(0.5f, 0, 1.5f); //1.5m away
            buttonKipas.SetScale(0.3f); //D=30cm

            // Scene has a lot of pre-configured components, such as Cameras (eyes), Lights, etc.
            DirectionalLight.Brightness = 1f;
            DirectionalLight.Node.SetDirection(new Vector3(-1, 0, 0.5f));

            var lampu = buttonLampu.CreateComponent<Box>();
            Debug.WriteLine("Earth ID = " + lampu.ID);
            lampu.Material = Material.FromImage("Textures/desk-lamp.png");

            var kipas = buttonKipas.CreateComponent<Box>();
            Debug.WriteLine("Moon ID = " + kipas.ID);
            kipas.Material = Material.FromImage("Textures/fan.png");

            // Media Capture Initialize
            mediaCapture = new MediaCapture();
            await mediaCapture.InitializeAsync();
            await mediaCapture.AddVideoEffectAsync(new MrcVideoEffectDefinition(), MediaStreamType.Photo);

            cTool = new CSHttpClientSample.CognitiveTool();

            await TextToSpeech("Home Automation");
            SimpleCreateInstructions("TUGAS BESAR TMDG 11\n23216310_Ferry S Suwita\n23216313_Pitra Dana A");

            await RegisterCortanaCommands(new Dictionary<string, Action> {
                    {"On", () => captureImage()},
                    {"Off", () => captureImage()},
                });
        }


        protected override void OnUpdate(float timeStep)
        {
            //rmq.WaitingMessage();
            //var dataTopic = rmq.getDataTopic();
            //var dataMessage = rmq.getDataMessage();
            //Debug.WriteLine("Topic:" + dataTopic + " ; message: " + dataMessage);
        }

        public override Vector3 FocusWorldPoint => buttonLampu.WorldPosition;

        Vector3 earthPosBeforeManipulations;
        bool surfaceIsValid;

        Boolean flag = false;

        public override void OnGestureTapped()
        {
            Ray cameraRay = RightCamera.GetScreenRay(0.5f, 0.5f);
            var result = Scene.GetComponent<Octree>().RaycastSingle(cameraRay, RayQueryLevel.Triangle, 100, DrawableFlags.Geometry, 0x70000000);
            if (result != null)
            {
                Debug.WriteLine("berhasil: " + result.Value.Node.GetComponent<Box>().ID);

                if (!flag)
                {
                    //rmq.SendMessage("led1", "0");
                    flag = true;
                }
                else
                {
                    //rmq.SendMessage("led1", "1");
                    flag = false;
                }
            }
        }

        async void captureImage()
        {
            if (busy) return;

            ShowBusyIndicator(true);
            var d = await doCaptureImage();
            InvokeOnMain(() => ShowBusyIndicator(false));

            String id = checkID(d);
            Debug.WriteLine("ID: " + id);

            if (id.Equals("0") || id.Equals("-1"))
            {
                await TextToSpeech("Sorry I can found the device");
            }
            else
            {
                if (!flag)
                {
                    rmq.SendMessage(id, "0");
                    flag = true;
                }
                else
                {
                    rmq.SendMessage(id, "1");
                    flag = false;
                }
            }
        }

        async Task<string> doCaptureImage()
        {
            var imgFormat = ImageEncodingProperties.CreateJpeg();

            var memoryStream = new MemoryStream();
            using (var ras = new InMemoryRandomAccessStream())
            {
                await mediaCapture.CapturePhotoToStreamAsync(imgFormat, ras);
                ras.Seek(0);
                using (var stream = ras.AsStreamForRead())
                    stream.CopyTo(memoryStream);
            }

            var imageBytes = memoryStream.ToArray();
            memoryStream.Position = 0;

            InvokeOnMain(() =>
            {
                var image = new Image();
                image.Load(new Urho.MemoryBuffer(imageBytes));

                Node child = Scene.CreateChild();
                child.Position = LeftCamera.Node.WorldPosition + LeftCamera.Node.WorldDirection * 2f;
                child.LookAt(LeftCamera.Node.WorldPosition, Vector3.Up, TransformSpace.World);

                child.Scale = new Vector3(1f, image.Height / (float)image.Width, 0.1f) / 10;
                var texture = new Texture2D();
                texture.SetData(image, true);

                var material = new Material();
                material.SetTechnique(0, CoreAssets.Techniques.Diff, 0, 0);
                material.SetTexture(TextureUnit.Diffuse, texture);

                var box = child.CreateComponent<Box>();
                box.SetMaterial(material);

                child.RunActions(new EaseBounceOut(new ScaleBy(1f, 5)));
            });

            var data = await cTool.ReadHandwrittenText(imageBytes);

            Debug.WriteLine(data.ToString());

            return data;
        }

        string checkID(string json)
        {
            try
            {
                //string json = "{'status': 'Succeeded','recognitionResult': {'lines': [{'boundingBox': [120,558,492,569,489,664,117,653],'text': 'Lampu 1','words': [{'boundingBox': [124,559,275,563,269,659,118,655],'text': 'ferry'},{'boundingBox': [317,564,506,568,500,664,311,660],'text': 'ganteng'}]}]}}";
                Debug.WriteLine(json);

                Response response = JsonConvert.DeserializeObject<Response>(json);
                Line[] line = response.recognitionResult.lines;
                string text = line[0].text;

                Debug.WriteLine(text);

                if (text.ToLower().Contains("1") || text.ToLower().Contains("i") || text.ToLower().Contains("lampu 1") || text.ToLower().Contains("lampu i") || text.ToLower().Contains("satu") || text.ToLower().Contains("led"))
                {
                    return "led1";
                }

                if (text.ToLower().Contains("1") || text.ToLower().Contains("i") || text.ToLower().Contains("kipas 1") || text.ToLower().Contains("kipas i") || text.ToLower().Contains("satu") || text.ToLower().Contains("fan"))
                {
                    return "led1";
                }

                if (text.ToLower().Contains("2") || text.ToLower().Contains("ii") || text.ToLower().Contains("lampu 2") || text.ToLower().Contains("lampu ii") || text.ToLower().Contains("dua") || text.ToLower().Contains("led"))
                {
                    return "led2";
                }

                if (text.ToLower().Contains("1") || text.ToLower().Contains("ii") || text.ToLower().Contains("kipas 2") || text.ToLower().Contains("kipas ii") || text.ToLower().Contains("dua") || text.ToLower().Contains("fan"))
                {
                    return "led2";
                }

                return "0";
            } catch (Exception e) {
                return "-1";
            }
        }

        void ShowBusyIndicator(bool show)
        {
            busy = show;
            busyIndicatorNode.Position = LeftCamera.Node.WorldPosition + LeftCamera.Node.WorldDirection * 1f;
            busyIndicatorNode.GetComponent<BusyIndicator>().IsBusy = show;
        }

        async Task<string> Capture()
        {
            var imgFormat = ImageEncodingProperties.CreateJpeg();
            var memoryStream = new MemoryStream();
            using (var ras = new InMemoryRandomAccessStream())
            {
                await mediaCapture.CapturePhotoToStreamAsync(imgFormat, ras);
                ras.Seek(0);
                using (var stream = ras.AsStreamForRead())
                    stream.CopyTo(memoryStream);
            }

            var imageBytes = memoryStream.ToArray();

            cTool = new CSHttpClientSample.CognitiveTool();
            string item = await cTool.ReadHandwrittenText(imageBytes);

            return item;
        }

        public override void OnGestureDoubleTapped()
        {
            captureImage();
        }

        Text textElement;

        protected void SimpleCreateInstructions(string text = "")
        {
            textElement = new Text()
            {
                Value = text,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            textElement.SetFont(ResourceCache.GetFont("Fonts/Anonymous Pro.ttf"), 15);
            UI.Root.AddChild(textElement);
        }

        public class MrcVideoEffectDefinition : IVideoEffectDefinition
        {
            public string ActivatableClassId => "Windows.Media.MixedRealityCapture.MixedRealityCaptureVideoEffect";

            public IPropertySet Properties { get; }

            public MrcVideoEffectDefinition()
            {
                Properties = new PropertySet
                {
                    {"HologramCompositionEnabled", false},
                    {"RecordingIndicatorEnabled", false},
                    {"VideoStabilizationEnabled", false},
                    {"VideoStabilizationBufferLength", 0},
                    {"GlobalOpacityCoefficient", 0.9f},
                    {"StreamType", (int)MediaStreamType.Photo}
                };
            }
        }
    }
}