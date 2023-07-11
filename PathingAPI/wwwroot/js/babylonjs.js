window.addEventListener('DOMContentLoaded', function () {

    const div = 10.0;
    const textures = ["grass.png", "waterbump.png", "floor.png", "ground.jpg"];

    var cameraPositionSet = false;
    var startedRendering = false;
    var modelId = 0;

    const layers = 4;
    var materials = new Array(layers);
    var rootNodes = new Array(layers);

    showAlert = (message) => {
        alert(message);
    }

    log = function (message) {
        console.log(message);
        document.getElementById('canvasText').innerHTML = message;
    }

    removeMeshes = function (name) {
        for (i = scene.meshes.length - 1; i >= 0; i--) {
            const mesh = scene.meshes[i];
            if (mesh.name === name)
                mesh.dispose();
        }
    }

    clear = function () {
        for (i = scene.meshes.length - 1; i >= 0; i--) {
            const mesh = scene.meshes[i];
            if (mesh.name !== "skyBox")
                mesh.dispose();
        }
        cameraPositionSet = false;
    }

    toggleWireFrame = function () {
        for (let i = 0; i < materials.length; i++) {
            materials[i].wireframe = !materials[i].wireframe;
        }
    }

    toggleLayer = function (layer) {
        // TriangleType 2^x => array index
        // Where TriangleType.None is excluded
        switch (layer) {
            case 1: layer = 0; break;
            case 2: layer = 1; break;
            case 4: layer = 2; break;
            case 8: layer = 3; break;
        }
        rootNodes[layer].setEnabled(!rootNodes[layer].isEnabled());
    }

    drawSphere = function (vector, color, name) {
        vector = JSON.parse(vector);

        removeMeshes(name);
        const sphere = BABYLON.Mesh.CreateSphere(name, 10.0, 0.5, scene, false, BABYLON.Mesh.DEFAULTSIDE);
        const material = new BABYLON.StandardMaterial(scene);
        material.alpha = 1;
        material.diffuseColor = getColour(color);
        sphere.material = material;
        sphere.position = new BABYLON.Vector3(vector.x / div, (vector.z / div) + getHeight(color), vector.y / div);

        //console.log("drawSphere: " + name + " completed.");
    }

    drawLine = function (vector, color, name) {
        vector = JSON.parse(vector);

        //log("drawLine: " + name);

        removeMeshes(name);
        const line1 = [
            new BABYLON.Vector3(vector.x / div, vector.z / div, vector.y / div),
            new BABYLON.Vector3(vector.x / div, (vector.z / div) + 10, vector.y / div)];

        const lines1 = BABYLON.MeshBuilder.CreateLines(name, { points: line1 }, scene);
        lines1.color = getColour(color);

        if (/*!cameraPositionSet || */name === "start") {
            cameraPositionSet = false;
            setCamera(vector, vector, 10);
        }

        //console.log("drawLine: " + name + " completed.");
    }

    var debugLineCount = 0;

    drawLineDebug = function (vector, color, name, count) {
        if (debugLineCount != count) {
            for (let i = debugLineCount; i >= 0; i--) {
                removeMeshes(name + i)
            }
            debugLineCount = count;
        }

        drawLine(vector, color, name);
    }

    getColour = function (color) {
        switch (color) {
            case 1: return BABYLON.Color3.Red();
            case 2: return BABYLON.Color3.Green();
            case 3: return BABYLON.Color3.Blue();
            case 5: return BABYLON.Color3.Teal();
            case 6: return new BABYLON.Color3(1, 0.6, 0);
            case 7: return BABYLON.Color3.Yellow();
            case 4:
            default: return BABYLON.Color3.White();
        }
    }

    getHeight = function (color) {
        switch (color) {
            case 2: return 0.5;
            case 4: return 0.1;
            case 7: return 0.1;
            default: return 0.11;
        }
    }

    drawPath = function (points, color, name) {
        points = JSON.parse(points);

        //log("drawPath: " + name);

        const path = [];
        for (i = 0; i < points.length; i++) {
            const p = points[i];
            const height = getHeight(color);
            path.push(new BABYLON.Vector3(p.x / div, (p.z / div) + height, p.y / div));
        }

        const lines = BABYLON.MeshBuilder.CreateLines(name, { points: path }, scene);
        lines.color = getColour(color);

        setCamera(points[0], points[points.length - 1], 20);

        //console.log("drawPath: " + name + " completed.");
    }

    createScene = function () {
        log("createScene: started");

        canvas = document.getElementById('renderCanvas');// get the canvas DOM element
        engine = new BABYLON.Engine(canvas, true); // load the 3D engine
        engine.setHardwareScalingLevel(0.5);

        scene = new BABYLON.Scene(engine);// create a basic BJS Scene object

        var light = new BABYLON.HemisphericLight("hemi", new BABYLON.Vector3(1, 1, 0), scene);
        light.intesity = 0.5;

        // the canvas/window resize event handler
        window.addEventListener('resize', function () { engine.resize(); });

        camera = new BABYLON.FreeCamera('camera1', new BABYLON.Vector3(0, 50, -0), scene);
        camera.keysUp.push(87);     // "w"
        camera.keysDown.push(83);   // "s"
        camera.keysLeft.push(65);   // "a"
        camera.keysRight.push(68);  // "d"
        camera.attachControl(canvas, false); // attach the camera to the canvas

        const cameraMinSpeed = 0.1;
        const cameraMaxSpeed = 1;
        camera.speed = cameraMinSpeed;

        // create layers
        for (let i = 0; i < layers; i++) {
            rootNodes[i] = new BABYLON.TransformNode();

            const mat = new BABYLON.StandardMaterial("mat" + i, scene);
            mat.diffuseTexture = new BABYLON.Texture("https://www.babylonjs-playground.com/textures/" + textures[i])
            mat.backFaceCulling = false;
            materials[i] = mat;
        }

        // Skybox
        const skybox = BABYLON.MeshBuilder.CreateBox("skyBox", { size: 4000.0 }, scene);
        const skyboxMaterial = new BABYLON.StandardMaterial("skyBox", scene);
        skyboxMaterial.backFaceCulling = false;
        skyboxMaterial.reflectionTexture = new BABYLON.CubeTexture("https://www.babylonjs-playground.com/textures/skybox", scene);
        skyboxMaterial.reflectionTexture.coordinatesMode = BABYLON.Texture.SKYBOX_MODE;
        skyboxMaterial.diffuseColor = new BABYLON.Color3(0, 0, 0);
        skyboxMaterial.specularColor = new BABYLON.Color3(0, 0, 0);
        skybox.material = skyboxMaterial;

        engine.runRenderLoop(function () {
            if (!scene.paused) {
                scene.render();
            }
        });

        var energy = 0, shiftPressed = false;
        scene.onBeforeRenderObservable.add(function () {
            if (shiftPressed) {
                camera.speed = cameraMaxSpeed;
                energy = 25;
            } else {
                if (energy > 0) {
                    energy--;
                }
                else {
                    camera.speed = cameraMinSpeed;
                }
            }
        });
        scene.onKeyboardObservable.add((kbInfo) => {
            switch (kbInfo.type) {
                case BABYLON.KeyboardEventTypes.KEYDOWN:
                    switch (kbInfo.event.key) {
                        case "Shift":
                            shiftPressed = true;
                            break;
                    }
                    break;

                case BABYLON.KeyboardEventTypes.KEYUP:
                    switch (kbInfo.event.key) {
                        case "Shift":
                            shiftPressed = false;
                            break;
                        case "o":
                            log("Camera Position: " + camera.position);
                            break;
                    }
            }
        });

        // Optimizer
        const options = BABYLON.SceneOptimizerOptions.HighDegradationAllowed(30);
        const optimizer = new BABYLON.SceneOptimizer(scene, options);
        optimizer.start();

        log("createScene: completed");
    };

    addModels = function (loadedIndices, loadedPositions) {
        //log("addModels: " + modelId);

        loadedIndices = JSON.parse(loadedIndices);
        loadedPositions = JSON.parse(loadedPositions);

        if (loadedPositions.length === 0) {
            return;
        }

        setCamera(loadedPositions[0], loadedPositions[loadedPositions.length - 1], 20);
        //cameraPositionSet = false;

        const positions = [];
        for (let i = 0; i < loadedPositions.length; i++) {
            positions.push(loadedPositions[i].x / div);
            positions.push(loadedPositions[i].z / div);
            positions.push(loadedPositions[i].y / div);
        }

        //take uv value relative to bottom left corner of roof (-4, -4) noting length and width of roof is 8. base uv value on the x, z coordinates only
        const uvs = [];
        for (let p = 0; p < positions.length / 3; p++) {
            uvs.push((positions[3 * p] - (-4)) / 4, (positions[3 * p + 2] - (-4)) / 4);
        }

        // add the models
        for (let p = 0; p < loadedIndices.length; p++) {
            const indices = loadedIndices[p];

            modelId++;
            const customMesh = new BABYLON.Mesh("custom" + modelId, scene);
            const normals = [];
            BABYLON.VertexData.ComputeNormals(positions, indices, normals);

            const vertexData = new BABYLON.VertexData();
            vertexData.positions = positions;
            vertexData.indices = indices;
            vertexData.normals = normals;
            vertexData.uvs = uvs;
            vertexData.applyToMesh(customMesh);
            //customMesh.convertToFlatShadedMesh();

            customMesh.material = materials[p];
            customMesh.parent = rootNodes[p];
        }

    }

    setCamera = function (pos, look, height) {
        if (cameraPositionSet) return;

        if (height === undefined) height = 0;

        const camera = scene.activeCamera;
        cameraPositionSet = true;
        camera.position = new BABYLON.Vector3(pos.x / div, (pos.z / div) + height, pos.y / div);
        camera.setTarget(new BABYLON.Vector3(look.x / div, look.z / div, look.y / div));
    }
});