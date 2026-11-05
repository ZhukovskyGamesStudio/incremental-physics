using System.Collections.Generic;
using System.IO;
using ChalkPhysics.Lab3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChalkPhysics.EditorTools
{
    /// Builds the 3D lab scene (Assets/Scenes/Lab3D.unity) from the Blender models in Assets/Lab3D/Models
    /// (made by Tools/Blender/lab3d.py): a 3D renderer with the chalk outline pass, the chalk materials, the room,
    /// the table with the crate of samples and the dynamometer, and the scientist with a camera.
    /// Menu: Chalk / Lab 3D. Headless: Tools/Lab3D/run.ps1 builds it and renders pictures into Build/Lab3D.
    public static class Lab3DBuilder
    {
        const string Root = "Assets/Lab3D";
        const string Models = Root + "/Models";
        const string Mats = Root + "/Materials";
        const string ScenePath = "Assets/Scenes/Lab3D.unity";
        const string RendererPath = "Assets/Settings/Renderer3D.asset";
        const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        const float TableTop = 1.21f;   // the tables are this high (Tools/Blender/lab3d.py, build_table)

        static readonly Color White = new Color(0.93f, 0.93f, 0.89f);
        static readonly Color Board = new Color(0.105f, 0.14f, 0.125f);
        static readonly Color Wood = new Color(0.95f, 0.74f, 0.5f);
        static readonly Color Pale = new Color(1f, 0.88f, 0.62f);
        static readonly Color Blue = new Color(0.45f, 0.86f, 1f);
        static readonly Color Green = new Color(0.55f, 1f, 0.6f);

        [MenuItem("Chalk/Lab 3D/Build scene")]
        public static void Build()
        {
            ConfigureModels();
            int renderer = EnsureRenderer();
            BuildScene(renderer);
        }

        [MenuItem("Chalk/Lab 3D/Render pictures")]
        public static void Pictures() => Shots(Path.GetFullPath("Build/Lab3D"));

        /// Batch entry: build, render the pictures, quit.
        public static void BuildBatch()
        {
            int code = 0;
            try
            {
                Build();
                Pictures();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ models and materials

        static void ConfigureModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter mi) continue;
                bool dirty = mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation
                             || mi.importCameras || mi.importLights || mi.animationType != ModelImporterAnimationType.None;
                if (!dirty) continue;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.SaveAndReimport();
            }
        }

        static Material Mat(string name, Color chalk, float fill, float hatch, float angle = 0.75f, float smudge = 0.2f, float ink = 0.85f)
        {
            Directory.CreateDirectory(Mats);
            string path = $"{Mats}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Chalk/Lab");
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.SetColor("_Chalk", chalk);
            m.SetColor("_Board", Board);
            m.SetFloat("_Fill", fill);
            m.SetFloat("_Hatch", hatch);
            m.SetFloat("_Angle", angle);
            m.SetFloat("_Smudge", smudge);
            m.SetFloat("_Ink", ink);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 at, Vector3 size, Material m)
        {
            var c = GameObject.CreatePrimitive(type);
            c.name = name;
            c.transform.SetParent(parent, false);
            c.transform.localPosition = at;
            c.transform.localScale = size;
            c.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(c.GetComponent<Collider>());
            return c;
        }

        static void Grip(GameObject go, Vector3 center, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        /// A Blender model of the desk or the room (Tools/Blender/desk.py: Models/Desk/<name>.fbx) under `parent`, at
        /// `at`: painted with the kit's chalk by the colour in each part's name ("Block.brown"), or with `by`'s material
        /// where a key of it begins the part's name or is its colour ("Paper" — the bare paper the writing goes on,
        /// "brown" — the lab's wood). A plain copy, not a prefab instance: its pieces get pivots (Art3D.Pivot).
        static Transform Model(string name, Transform parent, Vector3 at, PersonaKit kit, Dictionary<string, Material> by = null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Desk/{name}.fbx");
            var go = Object.Instantiate(src, parent, false);
            go.name = name;
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.identity;
            kit.Paint(go);
            if (by != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var parts = r.name.Split('.');
                    foreach (var kv in by)
                        if (parts[0].StartsWith(kv.Key) || (parts.Length > 1 && parts[1] == kv.Key)) { r.sharedMaterial = kv.Value; break; }
                }
            return go.transform;
        }

        /// The order desk's bureaucracy (DESIGN.md 2б, «Труба приёмки», «Посылка заказчику»): at the counter's left end a
        /// rubber stamp on its ink pad; at the right end the dispatch box with its two flaps and the tape that goes over
        /// them. Blender models (Tools/Blender/desk.py).
        static void BuildDesk(Transform counter, Lab3DGame game, PersonaKit kit, Material wood, Material metal)
        {
            var pad = Mat("InkPad", new Color(1f, 0.5f, 0.5f), 0.5f, 90, 0.2f, 0.3f);
            float top = TableTop;

            var inkPad = new GameObject("InkPad").transform;
            inkPad.SetParent(counter, false);
            inkPad.localPosition = new Vector3(-0.66f, top, -0.12f);
            Model("Desk_inkpad", inkPad, Vector3.zero, kit, new Dictionary<string, Material> { ["Pad"] = pad, ["metal"] = metal });
            Grip(inkPad.gameObject, new Vector3(0, 0.042f, 0), new Vector3(0.144f, 0.084f, 0.11f));   // a click on the pad puts the stamp back
            var stamp = new GameObject("Stamp").transform;
            stamp.SetParent(counter, false);
            stamp.localPosition = new Vector3(-0.66f, top + 0.012f, -0.12f);
            Model("Desk_stamp", stamp, Vector3.zero, kit, new Dictionary<string, Material> { ["brown"] = wood, ["metal"] = metal });
            Grip(stamp.gameObject, new Vector3(0, 0.065f, 0), new Vector3(0.09f, 0.13f, 0.07f));

            var box = new GameObject("DispatchBox").transform;
            box.SetParent(counter, false);
            box.localPosition = new Vector3(0.66f, top, 0.05f);
            const float wallH = 0.2f;   // tall enough for a ball of 2.8 l; a bottle goes in lying down
            var tapeMat = Mat("Tape", new Color(0.95f, 0.85f, 0.6f), 1.2f, 160, -0.4f, 0.2f);
            var boxModel = Model("Desk_box", box, Vector3.zero, kit, new Dictionary<string, Material> { ["Tape"] = tapeMat, ["brown"] = wood });
            // the lid: two flaps hinged on the side walls, standing open outwards; packing folds them shut one after the other
            var lid = new GameObject("Lid").transform;
            lid.SetParent(box, false);
            lid.localPosition = new Vector3(0, 0.005f + wallH, 0);
            foreach (var (name, x) in new[] { ("FlapL", -0.135f), ("FlapR", 0.135f) })
            {
                var flap = Art3D.Pivot(boxModel, name, lid);
                flap.localRotation = Quaternion.Euler(0, 0, x < 0 ? 115 : -115);
            }
            var inside = new GameObject("Inside").transform;
            inside.SetParent(box, false);
            inside.localPosition = new Vector3(0, 0.011f, 0);
            Grip(box.gameObject, new Vector3(0, 0.105f, 0), new Vector3(0.28f, 0.22f, 0.35f));
            // (no pipe and no button: the packed box goes back to its customer by hand, DESIGN.md 2б, «Посылка заказчику»)
            // the strip of tape over the shut lid and down the front: shown once the box is packed
            game.boxTape = Art3D.Pivot(boxModel, "Tape", box);

            game.stamp = stamp;
            game.calculator = BuildCalculator(counter.parent, kit);
            game.stampPad = inkPad;
            game.dispatchBox = box;
            game.boxLid = lid;
            game.boxInside = inside;
        }

        /// The calculator on the weighing table, by the counter: comes with the density. Keys in four rows of four, pressed
        /// with the dot; the labels and the display are written at runtime (Calculator3D).
        static GameObject BuildCalculator(Transform lab, PersonaKit kit)
        {
            var body = Mat("CalcBody", new Color(0.7f, 0.78f, 0.85f), 0, 40, 0.75f, 0);
            var keyMat = Mat("CalcKey", new Color(0.93f, 0.93f, 0.89f), 0, 40, 0.75f, 0);   // no strokes over the labels
            var screen = Mat("CalcScreen", Board, 0, 40, 0.75f, 0, 0);
            var root = new GameObject("Calculator");
            root.transform.SetParent(lab, false);
            root.transform.localPosition = new Vector3(0.9f, TableTop + 0.02f, 2.88f);
            root.transform.localRotation = Quaternion.Euler(-14, 0, 0);   // tipped up towards the one who stands at it
            var t = root.transform;
            var m = Model("Desk_calculator", t, Vector3.zero, kit, new Dictionary<string, Material> { ["Key"] = keyMat, ["Display"] = screen, ["Body"] = body });
            var display = Art3D.Pivot(m, "Display", t);
            string[] keys = { "7", "8", "9", "÷", "4", "5", "6", "×", "1", "2", "3", "−", "C", "0", ",", "=", "tg" };
            for (int i = 0; i < keys.Length; i++)
            {
                var k = Art3D.Pivot(m, $"Key{i:00}", t);   // with its collider: pressed with the dot
                k.name = "Key " + keys[i];
                k.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.031f, 0.008f, 0.029f);
                k.gameObject.AddComponent<CalcKey>().key = keys[i];
            }
            var calc = root.AddComponent<Calculator3D>();
            calc.display = display;
            return root;
        }

        /// A flat colour, no chalk strokes: for what must read exactly (the dynamometer's divisions).
        /// A see-through flat colour (URP Unlit, transparent, alpha blended): the water's body.
        static Material Clear(string name, Color c)
        {
            var m = Plain(name, c);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Plain(string name, Color c)
        {
            Directory.CreateDirectory(Mats);
            string path = $"{Mats}/{name}.mat";
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader != sh)
            {
                if (m != null) AssetDatabase.DeleteAsset(path);
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LinesMat()
        {
            Directory.CreateDirectory(Mats);
            string path = $"{Mats}/ChalkLines.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Chalk/Lines"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_Line", White);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// The 3D renderer: a plain Universal renderer with one full-screen pass, Chalk/Lines, that needs depth and
        /// normals. It is added to the pipeline asset next to the 2D renderer the rest of the game uses.
        static int EnsureRenderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, RendererPath);
            }
            FullScreenPassRendererFeature lines = null;
            foreach (var f in data.rendererFeatures) if (f is FullScreenPassRendererFeature fs) lines = fs;
            if (lines == null)
            {
                lines = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                lines.name = "ChalkLines";
                AssetDatabase.AddObjectToAsset(lines, data);
                data.rendererFeatures.Add(lines);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(lines, out _, out long id);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            lines.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            lines.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            lines.fetchColorBuffer = true;
            lines.passMaterial = LinesMat();
            lines.SetActive(true);
            EditorUtility.SetDirty(lines);
            EditorUtility.SetDirty(data);

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var pso = new SerializedObject(urp);
            var list = pso.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) { AssetDatabase.SaveAssets(); return i; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            pso.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return list.arraySize - 1;
        }

        // ------------------------------------------------------------------ the scene

        static GameObject Inst(string model, Transform parent, Vector3 pos)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{model}.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = pos;
            return go;
        }

        static void Paint(GameObject go, Dictionary<string, Material> byName, Material fallback)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterial = byName.TryGetValue(r.name, out var m) ? m : fallback;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void BoxAround(GameObject go)
        {
            // measured with the thing unturned: the box is in its own axes (a table turned 90° at build time had its box
            // across the top, and moved to another place it let the player through and stood in front of it)
            var t = go.transform;
            var rot = t.rotation;
            t.rotation = Quaternion.identity;
            var b = BoundsOf(go);
            var c = go.AddComponent<BoxCollider>();
            c.center = t.InverseTransformPoint(b.center);
            var s = t.lossyScale;
            c.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
            t.rotation = rot;
        }

        static void BuildScene(int rendererIndex)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            var wall = Mat("Wall", White, 0.4f, 30, 0.75f, 0.3f, 0.4f);
            var ceiling = Mat("Ceiling", White, 0.3f, 24, 0.75f, 0.3f, 0.35f);
            var floor = Mat("Floor", Wood, 0.45f, 40, 0.0f, 0.15f, 0.45f);
            var trim = Mat("Trim", White, 0.6f, 50, 0.75f, 0.2f);
            var wood = Mat("Wood", Wood, 0.7f, 45, 0.6f, 0.25f);
            var metal = Mat("Metal", Blue, 0.75f, 120, 0.9f, 0.2f);
            var spring = Mat("Spring", White, 1.4f, 200, 0.9f, 0.4f);
            var scale = Mat("Scale", White, 0.9f, 140, 0.0f, 0.3f);
            // the dynamometer's scale: a bare board plate with no strokes of light on it (they came and went with the sun
            // and hid the divisions), and plain white divisions on it
            var plate = Mat("ScalePlate", White, 0, 40, 0.0f, 0, 0);
            var tick = Plain("Tick", new Color(0.93f, 0.93f, 0.89f));
            var zero = Mat("Zero", Green, 1.5f, 200, 0.0f, 0.6f);
            var light = Mat("SampleLight", White, 0.35f, 150, 0.75f, 0.1f);
            var mid = Mat("SampleMid", White, 0.7f, 150, 0.75f, 0.2f);
            var dense = Mat("SampleDense", White, 1.15f, 150, 0.75f, 0.35f);
            // the kit's chalk colours: the customers and the Blender models made at runtime are painted with them, and the
            // desk's and the room's models put in here (Model)
            var kit = new PersonaKit
            {
                skin = Mat("PSkin", new Color(1f, 0.82f, 0.68f), 0.95f, 70, 0.75f, 0.2f),
                dark = Mat("PDark", new Color(0.35f, 0.33f, 0.32f), 0.6f, 120, 0.4f, 0.2f),
                white = Mat("PWhite", White, 0.9f, 70, 0.9f, 0.2f),
                red = Mat("PRed", new Color(1f, 0.5f, 0.45f), 0.95f, 70, 0.6f, 0.2f),
                blue = Mat("PBlue", new Color(0.55f, 0.7f, 1f), 0.95f, 70, -0.6f, 0.2f),
                green = Mat("PGreen", new Color(0.55f, 0.9f, 0.6f), 0.95f, 70, 0.3f, 0.2f),
                yellow = Mat("PYellow", new Color(1f, 0.9f, 0.45f), 0.95f, 70, 1.1f, 0.2f),
                brown = Mat("PBrown", new Color(0.85f, 0.62f, 0.42f), 0.95f, 70, -0.3f, 0.2f),
                grey = Mat("PGrey", new Color(0.75f, 0.75f, 0.75f), 0.9f, 70, 0.75f, 0.2f),
                orange = Mat("POrange", new Color(1f, 0.65f, 0.35f), 0.95f, 70, -1f, 0.2f),
                purple = Mat("PPurple", new Color(0.82f, 0.62f, 1f), 0.95f, 70, 0.2f, 0.2f),
                metal = metal,
                clear = Clear("PGlass", new Color(0.75f, 0.9f, 1f, 0.22f)),
                pink = Mat("PPink", new Color(1f, 0.62f, 0.72f), 1.0f, 80, 0.5f, 0.25f),
                water = Clear("PondWater", new Color(0.4f, 0.68f, 0.92f, 0.28f)),
                line = Plain("FishingLine", new Color(0.93f, 0.93f, 0.89f)),
            };

            var lab = new GameObject("Lab").transform;

            var sun = new GameObject("Light from the windows").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50, 30, 0);
            sun.transform.SetParent(lab, true);

            var room = Inst("Room", lab, Vector3.zero);
            // temporary walls, across the room and along it (DESIGN.md 2б, «Каморка»): at first the lab is a closet by the
            // door — the counter and the weighing table against the back wall, boxes piled in the corners, cobwebs; every
            // new department moves the walls back, clutter and all, and at last they go
            var partition = GameObject.CreatePrimitive(PrimitiveType.Cube);
            partition.name = "Partition";
            partition.transform.SetParent(lab, false);
            partition.GetComponent<MeshRenderer>().sharedMaterial = wall;
            var partitionSide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            partitionSide.name = "PartitionSide";
            partitionSide.transform.SetParent(lab, false);
            partitionSide.GetComponent<MeshRenderer>().sharedMaterial = wall;
            Transform Corner(string name) { var c = new GameObject(name).transform; c.SetParent(lab, false); return c; }
            var cornerFL = Corner("CornerFrontLeft");
            var cornerFR = Corner("CornerFrontRight");
            var cornerBL = Corner("CornerBackLeft");
            var cornerBR = Corner("CornerBackRight");
            cornerBR.localPosition = new Vector3(LabRoom.RightWall, 0, LabRoom.BackWall);
            LabRoom.Walls(partition.transform, partitionSide.transform, cornerFL, cornerFR, cornerBL, LabRoom.WallZ[0], LabRoom.WallX[0]);
            var cardboard = Mat("Cardboard", new Color(0.95f, 0.72f, 0.45f), 0.75f, 70, 0.4f, 0.25f);
            var tape = Mat("Tape", new Color(0.95f, 0.85f, 0.6f), 1.2f, 160, -0.4f, 0.2f);
            var web = Plain("Web", new Color(0.85f, 0.85f, 0.82f));
            // boxes piled in a corner, `dx`/`dz` the way into the room
            void Pile(Transform corner, float dx, float dz, int seed, bool deep)
            {
                var r = new System.Random(seed);
                float J() => (float)(r.NextDouble() * 2 - 1);
                void CardBox(Vector3 at, Vector3 size, float yaw)
                {
                    var b = new GameObject("Cardboard box");
                    b.transform.SetParent(corner, false);
                    b.transform.localPosition = at;
                    b.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    Model("Desk_cardbox", b.transform, Vector3.zero, kit, new Dictionary<string, Material> { ["Box"] = cardboard, ["Tape"] = tape }).localScale = size;
                    var col = b.AddComponent<BoxCollider>();   // it is in the way, things go on it
                    col.center = new Vector3(0, size.y / 2, 0);
                    col.size = size;
                }
                CardBox(new Vector3(dx * 0.32f, 0, dz * 0.28f), new Vector3(0.56f, 0.42f, 0.46f), 8 + 6 * J());
                CardBox(new Vector3(dx * 0.3f + 0.03f * J(), 0.42f, dz * 0.27f), new Vector3(0.42f, 0.32f, 0.38f), -14 + 8 * J());
                CardBox(new Vector3(dx * 0.34f, 0.74f, dz * 0.26f), new Vector3(0.3f, 0.22f, 0.28f), 20 * J());
                CardBox(new Vector3(dx * 0.82f, 0, dz * 0.25f), new Vector3(0.36f, 0.3f, 0.34f), 25 + 10 * J());
                if (deep) CardBox(new Vector3(dx * 0.3f, 0, dz * 0.74f), new Vector3(0.4f, 0.26f, 0.32f), -30 + 10 * J());
            }
            // a cobweb strung across an upper corner (a Blender model a metre across), `u`/`v` along the two walls from the
            // corner: turned so that the model's walls (+x, +z) lie along them (it is the same either way round)
            void Cobweb(Transform corner, Vector3 u, Vector3 v, float size)
            {
                var along = Vector3.Cross(Vector3.up, v) == u ? v : u;
                var t = Model("Desk_cobweb", corner, new Vector3(0, 3.0f, 0), kit, new Dictionary<string, Material> { ["Web"] = web });
                t.localRotation = Quaternion.LookRotation(along, Vector3.up);
                t.localScale = Vector3.one * size;
            }
            Pile(cornerFL, 1, 1, 3, true);
            Pile(cornerFR, -1, 1, 7, false);   // not up to the door
            Cobweb(cornerFL, Vector3.right, Vector3.forward, 0.42f);
            Cobweb(cornerFR, Vector3.left, Vector3.forward, 0.36f);
            Cobweb(cornerBL, Vector3.right, Vector3.back, 0.5f);
            Cobweb(cornerBR, Vector3.left, Vector3.back, 0.3f);
            // a bare bulb on a cord over the closet
            var bulbMat = Mat("Bulb", new Color(1f, 0.95f, 0.75f), 1.7f, 180, 0.75f, 0.2f);
            Model("Desk_lamp", lab, new Vector3(1.2f, 0, 2.25f), kit, new Dictionary<string, Material> { ["Bulb"] = bulbMat, ["Rose"] = bulbMat, ["metal"] = metal, ["dark"] = wall });
            Paint(room, new Dictionary<string, Material>
            {
                ["Floor"] = floor, ["Walls"] = wall, ["Ceiling"] = ceiling, ["Skirting"] = trim,
                ["WindowFrames"] = trim, ["DoorFrame"] = trim, ["Door"] = wood, ["Lamp"] = trim,
                ["Bulb"] = Mat("Bulb", new Color(1f, 0.95f, 0.75f), 1.7f, 180, 0.75f, 0.2f),
            }, wall);
            foreach (Transform c in room.transform)
            {
                if (c.name == "Floor") BoxAround(c.gameObject);
                if (c.name is "Walls" or "Door") c.gameObject.AddComponent<MeshCollider>();
            }

            var tableMat = Mat("Table", Pale, 0.75f, 55, -0.5f, 0.2f);
            GameObject Station(string name, Vector3 at, float yaw)
            {
                var st = new GameObject("Station " + name);
                st.transform.SetParent(lab, false);
                st.transform.localPosition = at;
                st.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var table = Inst("Table", st.transform, Vector3.zero);
                Paint(table, new Dictionary<string, Material>(), tableMat);
                BoxAround(table);
                return st;
            }

            // the counter by the door: the morning's order sheets lie on it, a sample on each (DESIGN.md 2б, «Листки-заказы»)
            var counterSt = Station("Counter", new Vector3(2.1f, 0, 3.0f), 0);
            // a board hung on the counter's front, under its top, on two cords: «заказы» is written on it at runtime
            var signMats = new Dictionary<string, Material> { ["Board"] = Mat("SignBoard", White, 0, 40, 0.75f, 0, 0), ["Cords-Cord"] = spring, ["metal"] = metal };
            var sign = new GameObject("Sign").transform;   // where the writing goes (Art3D.Signboard): the board's size, no mesh
            sign.SetParent(counterSt.transform, false);
            sign.localPosition = new Vector3(0, TableTop - 0.2f, -0.43f);
            sign.localScale = new Vector3(0.62f, 0.2f, 0.018f);
            Model("Desk_sign_counter", counterSt.transform, sign.localPosition, kit, signMats).name = "SignBoard";

            // a board hung on a table's front (`front` = ±1, the side of local z the room is on), under its top, on two cords
            void Hang(Transform st, float front, float width, float height)
            {
                var board = new GameObject("Sign").transform;   // where the writing goes: the board's size, no mesh
                board.SetParent(st, false);
                board.localPosition = new Vector3(0, TableTop - 0.07f - height / 2, front * 0.43f);
                board.localRotation = Quaternion.Euler(0, front > 0 ? 180 : 0, 0);
                board.localScale = new Vector3(width, height, 0.018f);
                var m = Model("Desk_sign_dept", st, board.localPosition, kit, signMats);   // 0.5 × 0.16 m, as every department's
                m.name = "SignBoard";
                m.localRotation = board.localRotation;
            }

            Apparatus3D MakeDyna(Transform parent)
            {
                var go = Inst("Dynamometer", parent, new Vector3(0, TableTop, -0.1f));
                Paint(go, new Dictionary<string, Material>
                {
                    ["Stand"] = metal, ["Spring"] = spring, ["Hook"] = metal, ["Scale"] = plate, ["ZeroMark"] = zero,
                }, metal);
                BoxAround(go);
                var d = go.AddComponent<Dynamometer3D>();
                d.spring = go.transform.Find("Spring");
                d.hook = go.transform.Find("Hook");
                d.zeroMark = go.transform.Find("ZeroMark").GetComponent<Renderer>();
                d.tickMat = tick;
                d.lensRenderer = rendererIndex;
                return d;
            }
            Apparatus3D MakeShelf(Transform parent)
            {
                var go = Inst("Shelf", parent, new Vector3(0, TableTop, -0.1f));
                Paint(go, new Dictionary<string, Material> { ["Ruler"] = scale, ["Carriage"] = trim, ["Stake"] = wood, ["ImpactScale"] = plate, ["ImpactZero"] = zero }, metal);
                BoxAround(go);
                var sh = go.AddComponent<Shelf3D>();
                sh.carriage = go.transform.Find("Carriage");
                sh.stake = go.transform.Find("Stake");
                sh.zeroMark = go.transform.Find("ImpactZero").GetComponent<Renderer>();
                sh.tickMat = tick;
                return sh;
            }
            // the bath, V = Sh (DESIGN.md 2б, «Ванночка»): an overflow can on a low stand, open to the front like a drawing of
            // one, its spout over a measuring cylinder on the table; the water is the game's (balls it never draws)
            var glass = Mat("Glass", new Color(0.75f, 0.9f, 1f), 0.12f, 60, 0.9f, 0.05f, 0.4f);
            var waterBody = Clear("WaterBody", new Color(0.4f, 0.68f, 0.95f, 0.32f));
            var waterEdge = Plain("WaterEdge", new Color(0.82f, 0.94f, 1f));
            var tubeGlass = Clear("CylinderGlass", new Color(0.85f, 0.95f, 1f, 0.16f));
            Apparatus3D MakeBath(Transform parent)
            {
                var go = new GameObject("Bath");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0, TableTop, 0);
                var t = go.transform;
                const float stand = 0.2f, w = 0.34f, hgt = 0.3f, dp = 0.26f, x0 = -0.1f, cx = 0.215f;
                // the glass and the metal: a Blender model (Tools/Blender/desk.py, overflow); the water is Bath3D's
                Model("Desk_overflow", t, Vector3.zero, kit, new Dictionary<string, Material>
                {
                    ["Tube"] = tubeGlass, ["Can"] = glass, ["Spout"] = glass, ["Foot"] = glass, ["Rim"] = glass, ["metal"] = metal,
                });
                float floor = stand + 0.012f;   // the inside of the can's bottom
                const float spout = 0.2f;
                var tip = new GameObject("SpoutTip").transform;
                tip.SetParent(t, false);
                tip.localPosition = new Vector3(cx - 0.01f, floor + spout - 0.016f, 0);
                // the measuring cylinder on the table: a hundred square centimetres inside, ten centimetres a litre
                const float r = 0.0564f, ch = 0.37f;
                var cylBase = new GameObject("CylinderBase").transform;
                cylBase.SetParent(t, false);
                cylBase.localPosition = new Vector3(cx, 0.012f, 0);
                var z0 = Part(PrimitiveType.Cube, "ZeroMark", t, new Vector3(cx + 0.02f, 0.012f, r + 0.008f), new Vector3(0.026f, 0.003f, 0.004f), zero);
                var seat = new GameObject("Seat").transform;
                seat.SetParent(t, false);
                seat.localPosition = new Vector3(x0, floor, 0);
                BoxAround(go);
                var b = go.AddComponent<Bath3D>();
                b.seat = seat;
                b.cylBase = cylBase;
                b.spoutTip = tip;
                b.zeroMark = z0.GetComponent<Renderer>();
                b.tickMat = tick;
                b.bodyMat = waterBody;
                b.edgeMat = waterEdge;
                b.inner = new Vector3(w, hgt, dp);
                b.spout = spout;
                b.cylRadius = r;
                b.cylHeight = ch;
                return b;
            }
            Apparatus3D MakeIncline(Transform parent)
            {
                var go = new GameObject("Incline");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0, TableTop, 0.05f);
                var t = go.transform;
                const float len = 0.6f, hingeX = 0.3f, hingeY = 0.06f;
                // a Blender model (Tools/Blender/desk.py, incline): the base, the hinge, the board with its stop and needle
                // (it turns about the hinge), a clear protractor on a bar
                var m = Model("Desk_incline", t, Vector3.zero, kit, new Dictionary<string, Material> { ["Board-Needle"] = tick, ["brown"] = wood, ["metal"] = metal });
                var pivot = Art3D.Pivot(m, "Board", t);
                pivot.name = "Pivot";
                var seat = new GameObject("Seat").transform;
                seat.SetParent(pivot, false);
                seat.localPosition = new Vector3(-len + 0.11f, 0.02f, 0);
                var stop = new GameObject("StopSeat").transform;
                stop.SetParent(pivot, false);
                stop.localPosition = new Vector3(-0.1f, 0.02f, 0);
                // the protractor: an arc round the hinge on the board's front side, its 0° by the board at rest
                var z0 = Part(PrimitiveType.Cube, "ZeroMark", t, new Vector3(hingeX - 0.2f - 0.015f, hingeY + 0.021f, 0.115f), new Vector3(0.03f, 0.004f, 0.004f), zero);
                BoxAround(go);
                var inc = go.AddComponent<Incline3D>();
                inc.pivot = pivot;
                inc.seat = seat;
                inc.stop = stop;
                inc.zeroMark = z0.GetComponent<Renderer>();
                inc.tickMat = tick;
                return inc;
            }
            Apparatus3D MakeLauncher(Transform parent)
            {
                var go = Inst("Launcher", parent, new Vector3(0, TableTop, -0.1f));
                Paint(go, new Dictionary<string, Material> { ["Ruler"] = scale, ["LaunchSpring"] = spring, ["Plate"] = trim }, metal);
                BoxAround(go);
                var l = go.AddComponent<Launcher3D>();
                l.plate = go.transform.Find("Plate");
                l.spring = go.transform.Find("LaunchSpring");
                return l;
            }
            Apparatus3D MakePendulum(Transform parent)
            {
                var go = Inst("Pendulum", parent, new Vector3(0, TableTop, -0.1f));
                Paint(go, new Dictionary<string, Material> { ["Protractor"] = scale, ["Thread"] = spring }, metal);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);   // to hang the thread on a pivot
                var thread = go.transform.Find("Thread");
                var pivot = new GameObject("Pivot").transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = thread.localPosition;
                thread.SetParent(pivot, false);
                thread.localPosition = Vector3.zero;
                thread.localScale = new Vector3(1, 0.52f, 1);
                var bob = new GameObject("Bob").transform;
                bob.SetParent(pivot, false);
                bob.localPosition = Vector3.down * 0.52f;
                BoxAround(go);
                var pd = go.AddComponent<Pendulum3D>();
                pd.pivot = pivot;
                pd.thread = thread;
                pd.bob = bob;
                return pd;
            }

            // the departments (DESIGN.md 2б, «Отделы»): a table of one formula each, with room for the apparatus the map
            // adds, and the helpers' way to the back of it from the counter, round the other tables
            var counterFront = new Vector3(2.1f, 0, 2.25f);
            var departments = new List<Department>();
            var all = new List<Apparatus3D>();
            void Dept(string id, string formula, string name, Vector3 at, float yaw, int n, System.Func<Transform, Apparatus3D> make, params Vector3[] path)
            {
                var st = Station(name, at, yaw);
                Hang(st.transform, yaw > 90 || id == "incline" ? 1 : -1, 0.5f, 0.16f);
                var d = st.AddComponent<Department>();
                d.id = id;
                d.table = st.transform;
                d.path = new List<Vector3> { counterFront };
                d.path.AddRange(path);
                for (int i = 0; i < n; i++)
                {
                    var a = make(st.transform);
                    a.id = id;
                    a.formula = formula;
                    a.station = st;
                    a.copy = i;
                    if (i > 0) a.name += " " + (i + 1);
                    d.apparatus.Add(a);
                }
                // side by side, the first in the middle: its bounds centred on its place
                var b0 = BoundsOf(d.apparatus[0].gameObject);
                float w = b0.size.x + 0.12f, cx = st.transform.InverseTransformPoint(b0.center).x;
                for (int i = 0; i < n; i++)
                {
                    float x = n == 2 ? (i - 0.5f) * w : i == 0 ? 0 : i == 1 ? -w : w;
                    var t = d.apparatus[i].transform;
                    t.localPosition = new Vector3(x - cx, t.localPosition.y, t.localPosition.z);
                }
                all.AddRange(d.apparatus);
                departments.Add(d);
            }
            // the weighing department faces the room, with its back to the board
            // the weighing table against the back wall of the closet, beside the counter, facing the room
            Dept("dyna", "F = mg", "Dynamometers", new Vector3(0.25f, 0, 3.1f), 180, 3, MakeDyna, new Vector3(1.0f, 0, 2.35f));
            // the bath against the back wall too, left of the weighing table: the closet widens for it
            Dept("bath", "V = Sh", "Bath", new Vector3(-1.4f, 0, 3.1f), 180, 1, MakeBath, new Vector3(1.0f, 0, 2.35f));
            // the inclined plane in the middle of the room, facing the closet: the third department widens it back
            Dept("incline", "μ = tg α", "Incline", new Vector3(-0.6f, 0, -0.1f), 0, 1, MakeIncline, new Vector3(1.0f, 0, 2.35f));
            // (the shelf, the spring and the pendulum are not in the 3D lab's tree: they measure nothing about a sample)

            // the odd measurements (DESIGN.md 2б, «Крейзи-измерения»): a department each, its table (or the pond's tub) put
            // in its place at runtime in the order it is bought (LabRoom.Spots); their things are built at runtime (Art3D)
            void OddDept<T>(string id, string name, bool table, int spot) where T : Apparatus3D
            {
                var (at, yaw) = LabRoom.Spots[spot];
                GameObject st;
                if (table) st = Station(name, at, yaw);
                else
                {
                    st = new GameObject("Station " + name);
                    st.transform.SetParent(lab, false);
                    st.transform.localPosition = at;
                    st.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                }
                Hang(st.transform, 1, 0.5f, 0.16f);
                if (!table)
                {   // no table: the sign is nailed onto the tub's front, its left half, without its cords
                    foreach (var n in new[] { "Sign", "SignBoard" }) st.transform.Find(n).localPosition = new Vector3(-0.38f, 0.27f, 0.371f);
                    foreach (Transform c in st.transform.Find("SignBoard"))
                        if (c.name.StartsWith("Cords-")) c.gameObject.SetActive(false);
                }
                var d = st.AddComponent<Department>();
                d.id = id;
                d.table = st.transform;
                var go = new GameObject(name);
                go.transform.SetParent(st.transform, false);
                go.transform.localPosition = new Vector3(0, table ? TableTop : 0, 0);
                var a = go.AddComponent<T>();
                a.id = id;
                a.formula = "";
                a.station = st;
                d.apparatus.Add(a);
                all.Add(a);
                departments.Add(d);
            }
            OddDept<Tongue3D>("taste", "Tongue", true, 3);
            OddDept<Rod3D>("fish", "Pond", false, 4);
            OddDept<Mirror3D>("mirror", "Mirror", true, 5);
            OddDept<Cat3D>("cat", "Cat", true, 6);
            OddDept<Xylo3D>("note", "Xylophone", true, 7);
            OddDept<Feather3D>("tickle", "Feather", true, 8);

            var boardGo = Inst("Board", lab, new Vector3(0, 0, 3.5f));
            Paint(boardGo, new Dictionary<string, Material>
            {
                ["BoardPanel"] = Mat("BoardPanel", White, 0.14f, 30, 0.2f, 0.7f, 0.3f), ["BoardFrame"] = wood,
            }, wood);
            var board = boardGo.AddComponent<Board3D>();
            boardGo.SetActive(false);   // no board in the closet for now: it did nothing

            // the wall clock over the board: the day's time
            var clock = Inst("Clock", lab, new Vector3(0, 2.65f, 3.5f));
            Paint(clock, new Dictionary<string, Material> { ["HourHand"] = spring, ["MinuteHand"] = spring, ["ClockPin"] = metal }, trim);
            BoxAround(clock);   // a certificate is not hung over it

            // the order sheets: eight places on the counter in one row from the middle out, each a sheet of paper with a
            // few lines of writing and a sample lying on it (size is volume, hatching density)
            var paper = Mat("Paper", White, 0, 40, 0.75f, 0, 0);   // bare board: no strokes of light over the writing
            var kinds = new (float v, float m)[] { (1, 1), (2, 1), (0.5f, 1), (1, 2), (3, 3), (0.5f, 0.5f), (1, 0.5f), (2, 2) };
            var samples = new List<Sample3D>();
            var sheets = new List<OrderSheet>();
            GameObject Cube(string name, Transform parent, Vector3 at, Vector3 size, Material m, bool collide)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = name;
                c.transform.SetParent(parent, false);
                c.transform.localPosition = at;
                c.transform.localScale = size;
                c.GetComponent<MeshRenderer>().sharedMaterial = m;
                if (!collide) Object.DestroyImmediate(c.GetComponent<Collider>());
                return c;
            }
            for (int i = 0; i < kinds.Length; i++)
            {
                var sheetGo = new GameObject("Sheet " + (i + 1));
                sheetGo.transform.SetParent(counterSt.transform, false);
                int place = i % 4;   // two rows of four, the front row first, each from the middle out
                sheetGo.transform.localPosition = new Vector3((place % 2 == 0 ? -1 : 1) * (0.13f + 0.26f * (place / 2)), TableTop, i < 4 ? -0.17f : 0.18f);
                sheetGo.transform.localRotation = Quaternion.Euler(0, (i * 37 % 9) - 4, 0);   // laid down by hand, a little askew
                // the leaf (the paper and, at runtime, the writing on it) flies up to the eyes; the seat stays on the counter
                var leaf = new GameObject("Leaf").transform;
                leaf.SetParent(sheetGo.transform, false);
                Cube("Paper", leaf, new Vector3(0, 0.0015f, 0), new Vector3(0.225f, 0.003f, 0.3f), paper, true);
                var slot = new GameObject("Seat").transform;
                slot.SetParent(sheetGo.transform, false);
                slot.localPosition = new Vector3(0, 0.003f, -0.075f);   // on the sheet's lower half, in the middle
                var sheet = sheetGo.AddComponent<OrderSheet>();
                sheet.seat = slot;
                sheet.leaf = leaf;
                var grip = sheetGo.AddComponent<BoxCollider>();   // a sheet is easy to pick up: more than its 3 mm of paper
                grip.center = new Vector3(0, 0.012f, 0);
                grip.size = new Vector3(0.245f, 0.024f, 0.32f);
                sheets.Add(sheet);
                var go = Inst("Sample", slot, Vector3.zero);
                var k = kinds[i];
                float rho = k.m / k.v;
                go.GetComponent<MeshRenderer>().sharedMaterial = rho < 1 ? light : rho > 1 ? dense : mid;
                go.name = $"Sample V={k.v} m={k.m}";
                var smp = go.AddComponent<Sample3D>();
                var rb = go.AddComponent<Rigidbody>();   // laid down anywhere it falls and stacks (kinematic while carried)
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;   // smoothed only while it lies free (Sample3D.Body)
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.mass = k.m;
                smp.volume = k.v;
                smp.mass = k.m;
                smp.home = slot;
                smp.Shape();
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.5f, 0);
                col.size = Vector3.one;   // just the sample: the sheet under it is easy to aim at
                samples.Add(smp);
                sheet.sample = smp;
            }

            // the chalk helpers (DESIGN.md 2б, «Мелки-помощники»): five at most, by the counter until the map hires them
            var chalkie = Mat("Chalkie", White, 0.85f, 120, 0.75f, 0.25f);
            var eyes = Mat("Eyes", Board, 1.6f, 200, 0.75f, 0.1f, 0.2f);
            var helpers = new List<Helper3D>();
            for (int i = 0; i < 5; i++)
            {
                var h = new GameObject("Helper " + (i + 1));
                h.transform.SetParent(lab, false);
                var rest = new Vector3(1.3f + 0.28f * i, 0, 1.95f);
                h.transform.localPosition = rest;
                var bodyGo = Inst("Chalkie", h.transform, Vector3.zero);
                Paint(bodyGo, new Dictionary<string, Material> { ["Eyes"] = eyes }, chalkie);
                var carry = new GameObject("Carry").transform;
                carry.SetParent(h.transform, false);
                carry.localPosition = new Vector3(0, 0.43f, 0);
                var hp = h.AddComponent<Helper3D>();
                hp.body = bodyGo.transform;
                hp.carry = carry;
                hp.rest = rest;
                h.SetActive(false);
                helpers.Add(hp);
            }

            // the customers (DESIGN.md 2б, «Заказчики»): outside the lab, in a corridor behind the window over the counter;
            // they are Blender models (Tools/Blender/customers.py) put in at runtime (Persona), painted in the kit's colours
            var customers = new List<Customer3D>();
            for (int i = 0; i < 6; i++)
            {
                var c = new GameObject("Customer " + (i + 1));
                c.transform.SetParent(lab, false);
                c.transform.localPosition = new Vector3(4.4f, 0, 4.12f);
                var lookT = new GameObject("Look").transform;
                lookT.SetParent(c.transform, false);
                var cap = c.AddComponent<CapsuleCollider>();   // the dot finds it through the window
                cap.radius = 0.3f;
                cap.height = 1.0f;
                cap.center = new Vector3(0, 1.45f, 0);
                var cu = c.AddComponent<Customer3D>();
                cu.look = lookT;
                c.SetActive(false);
                customers.Add(cu);
            }
            // the corridor they come along: a floor, a far wall, a ceiling, ends; and a shelf of a sill under the window
            {
                var corr = new GameObject("Corridor").transform;
                corr.SetParent(lab, false);
                Part(PrimitiveType.Cube, "CorridorFloor", corr, new Vector3(1.8f, -0.03f, 4.35f), new Vector3(5.4f, 0.06f, 1.4f), floor);
                Part(PrimitiveType.Cube, "CorridorWall", corr, new Vector3(1.8f, 1.5f, 5.1f), new Vector3(5.4f, 3f, 0.1f), wall);
                Part(PrimitiveType.Cube, "CorridorCeiling", corr, new Vector3(1.8f, 3.03f, 4.35f), new Vector3(5.4f, 0.06f, 1.4f), ceiling);
                Part(PrimitiveType.Cube, "CorridorEndL", corr, new Vector3(-0.95f, 1.5f, 4.35f), new Vector3(0.1f, 3f, 1.4f), wall);
                Part(PrimitiveType.Cube, "CorridorEndR", corr, new Vector3(4.55f, 1.5f, 4.35f), new Vector3(0.1f, 3f, 1.4f), wall);
                // the sill on its brackets, a little over the opening's bottom: never in one plane with it
                Model("Desk_sill", corr, new Vector3(CustomerLine.Window.x, TableTop - 0.004f, 3.58f), kit, new Dictionary<string, Material> { ["brown"] = wood });
            }

            var who = new GameObject("Scientist");
            who.transform.position = new Vector3(1.3f, 0.02f, 1.35f);
            who.transform.rotation = Quaternion.Euler(0, 12, 0);   // in the closet: the weighing table and the counter in view
            var cc = who.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0, 0.85f, 0);
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(who.transform, false);
            eyeGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            eyeGo.transform.localRotation = Quaternion.Euler(12, 0, 0);
            var eye = eyeGo.AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Board * 0.8f;
            eye.nearClipPlane = 0.03f;
            eye.farClipPlane = 60;
            eye.fieldOfView = 62;
            eyeGo.tag = "MainCamera";
            eyeGo.AddComponent<AudioListener>();
            var camData = eye.GetUniversalAdditionalCameraData();
            camData.SetRenderer(rendererIndex);
            camData.renderPostProcessing = true;   // for the anti-aliasing only: no volume, no other effects
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            var walker = who.AddComponent<LabWalker>();
            walker.eye = eye;

            var game = new GameObject("Lab3D").AddComponent<Lab3DGame>();
            {   // the models made at runtime (customers...): every FBX under Models, found by its file name (ModelLibrary)
                var lib = game.gameObject.AddComponent<ModelLibrary>();
                var paths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                paths.Sort(System.StringComparer.Ordinal);
                foreach (var path in paths) { var mdl = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (mdl != null) lib.models.Add(mdl); }
            }
            game.walker = walker;
            game.eye = eye;
            game.crate = counterSt.transform;
            game.sheets = sheets;
            game.counter = counterSt.transform;
            game.counterSign = sign.transform;
            BuildDesk(counterSt.transform, game, kit, wood, metal);
            game.partition = partition.transform;
            game.partitionSide = partitionSide.transform;
            game.cornerFL = cornerFL;
            game.cornerFR = cornerFR;
            game.cornerBL = cornerBL;
            game.departments = departments;
            game.helpers = helpers;
            game.customers = customers;
            game.kit = kit;
            foreach (var cu in customers) cu.game = game;
            foreach (var h in helpers) h.game = game;
            game.board = board;
            game.samples = samples;
            game.apparatus = all;
            game.door = room.transform.Find("Door");
            {
                var db = game.door.GetComponent<Renderer>().bounds;
                var centre = lab.position;
                bool thinX = db.size.x < db.size.z;
                Vector3 dir = thinX ? new Vector3(Mathf.Sign(centre.x - db.center.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(centre.z - db.center.z));
                Vector3 face = thinX ? new Vector3(dir.x > 0 ? db.max.x : db.min.x, db.min.y + 1.0f, db.min.z + 0.12f)
                                     : new Vector3(db.min.x + 0.12f, db.min.y + 1.0f, dir.z > 0 ? db.max.z : db.min.z);
                var knob = Model("Desk_doorknob", lab, lab.InverseTransformPoint(face), kit, new Dictionary<string, Material> { ["Knob"] = metal });
                knob.rotation = Quaternion.LookRotation(dir, Vector3.up);   // out of the door, into the room
            }
            game.sampleMats = new[] { light, mid, dense };
            {   // a certificate (DESIGN.md 2б, «Грамоты»): a golden frame round a sheet of paper, on a cord from a nail; hidden —
                // one is made of it for every achievement, and its writing and picture are drawn at runtime
                var cert = new GameObject("Certificate");
                cert.transform.SetParent(lab, false);
                var gold = Mat("FrameGold", new Color(1f, 0.85f, 0.45f), 0.65f, 70, 0.6f, 0.2f);
                var bare = Mat("Paper", White, 0, 40, 0.75f, 0, 0);
                Model("Desk_certificate", cert.transform, Vector3.zero, kit, new Dictionary<string, Material> { ["Paper"] = bare, ["Frame"] = gold, ["Cord"] = spring, ["metal"] = metal });
                var col = cert.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0, 0.002f);
                col.size = new Vector3(0.344f, 0.444f, 0.026f);
                cert.SetActive(false);
                game.certTemplate = cert.transform;
            }
            game.linesMat = LinesMat();
            foreach (var a in all) if (a is Mirror3D mr) mr.lensRenderer = rendererIndex;
            {   // glass in the room's windows, four panes each (the window frames' cross between them): a thrown sample breaks one
                void Window(Vector3 centre, Vector3 right, Vector3 into)
                {
                    for (int qx = -1; qx <= 1; qx += 2)
                        for (int qy = -1; qy <= 1; qy += 2)
                        {
                            var p = new GameObject("Pane");
                            p.transform.SetParent(lab, false);
                            p.transform.localPosition = centre + right * (qx * 0.33f) + Vector3.up * (qy * 0.38f);
                            p.transform.localRotation = Quaternion.LookRotation(into, Vector3.up);
                            Part(PrimitiveType.Cube, "Glass", p.transform, Vector3.zero, new Vector3(0.6f, 0.71f, 0.006f), kit.clear);
                            var g1 = Part(PrimitiveType.Cube, "Gleam", p.transform, new Vector3(-0.12f, 0.12f, 0.005f), new Vector3(0.004f, 0.16f, 0.002f), tick);
                            g1.transform.localRotation = Quaternion.Euler(0, 0, 35);
                            var g2 = Part(PrimitiveType.Cube, "Gleam", p.transform, new Vector3(-0.07f, 0.1f, 0.005f), new Vector3(0.004f, 0.09f, 0.002f), tick);
                            g2.transform.localRotation = Quaternion.Euler(0, 0, 35);
                            var col = p.AddComponent<BoxCollider>();
                            col.size = new Vector3(0.62f, 0.73f, 0.04f);
                            var wp = p.AddComponent<WindowPane>();
                            wp.game = game;
                            wp.shardMat = tick;
                        }
                }
                Window(new Vector3(1.4f, 1.65f, -3.575f), Vector3.right, Vector3.forward);
                Window(new Vector3(-1.4f, 1.65f, -3.575f), Vector3.right, Vector3.forward);
                Window(new Vector3(-3.075f, 1.65f, -0.6f), Vector3.forward, Vector3.right);
            }
            {   // the light switch by the door (the odd measurement «glows in the dark»): a plate, its key, a green dot
                var sw = new GameObject("Light switch");
                sw.transform.SetParent(lab, false);
                sw.transform.localPosition = new Vector3(LabRoom.RightWall - 0.01f, 1.3f, 1.2f);
                sw.transform.localRotation = Quaternion.Euler(0, -90, 0);   // its face looks into the room
                var m = Model("Desk_switch", sw.transform, Vector3.zero, kit, new Dictionary<string, Material> { ["Plate"] = trim, ["metal"] = metal });
                var toggle = Art3D.Pivot(m, "Toggle", sw.transform);
                foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    if (r.name.StartsWith("Glow-")) r.gameObject.layer = Lab3DGame.GlowLayer;   // seen in the dark: where the switch is
                var col = sw.AddComponent<BoxCollider>();
                col.size = new Vector3(0.14f, 0.18f, 0.06f);
                var ls = sw.AddComponent<LightSwitch>();
                ls.game = game;
                ls.toggle = toggle;
                game.lightSwitch = sw.transform;
                sw.SetActive(false);
            }
            var day = game.gameObject.AddComponent<LabDay>();
            day.sun = sun;
            day.walker = walker;
            day.hourHand = clock.transform.Find("HourHand");
            day.minuteHand = clock.transform.Find("MinuteHand");
            game.day = day;
            foreach (var a in game.apparatus)
            {
                a.game = game;
                if (a.GetComponentsInChildren<Renderer>().Length > 0) a.labelHeight = BoundsOf(a.gameObject).max.y - a.transform.position.y + 0.09f;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            // the map hands over to the day by the scene's name: it has to be in the build
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(x => x.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ pictures

        static void Shots(string dir)
        {
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(dir);
            var walker = Object.FindAnyObjectByType<LabWalker>();
            var game = Object.FindAnyObjectByType<Lab3DGame>();
            var samples = game.samples;
            Apparatus3D First(string id) => game.departments.Find(d => d.id == id).apparatus[0];
            var dyn = (Dynamometer3D)First("dyna");
            var eye = walker.eye;

            void Shot(string name, Vector3 at, float yaw, float pitch)
            {
                walker.transform.position = at;
                walker.transform.rotation = Quaternion.Euler(0, yaw, 0);
                eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(eye, req)) RenderPipeline.SubmitRenderRequest(eye, req);
                else { eye.targetTexture = rt; eye.Render(); eye.targetTexture = null; }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Debug.Log("[LAB3D] picture " + name);
            }

            var start = walker.transform.position;
            float startYaw = walker.transform.eulerAngles.y;
            Shot("2_start", start, startYaw, 12);
            Shot("1_room", new Vector3(2.6f, 0.02f, 1.2f), 235, 10);
            Shot("7_counter", new Vector3(2.1f, 0.02f, 1.7f), 0, 28);
            dyn.HangAtRest(samples[4]);
            Shot("3_dynamometer", new Vector3(-1.9f, 0.02f, 0.95f), 0, 22);
            Shot("4_back", new Vector3(-1.2f, 0.02f, -2.6f), 30, 8);
            Shot("6_board", new Vector3(0, 0.02f, 1.0f), 0, 0);
            Shot("11_lab", new Vector3(0.1f, 0.02f, -0.2f), 300, 14);
            EditorSceneManager.OpenScene(ScenePath);   // drop the hung sample
        }
    }
}
