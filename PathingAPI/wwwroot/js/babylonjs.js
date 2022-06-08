window.addEventListener('DOMContentLoaded', function () {

    const div = 10.0;

    showAlert = (message) => {
        alert(message);
    }

    log = function (message) {
        console.log(message);
        document.getElementById('canvasText').innerHTML = message;
    }

    removeMeshes = function (name) {
        for (i = scene.meshes.length - 1; i >= 0; i--) {
            var mesh = scene.meshes[i];
            if (mesh.name === name) { mesh.dispose(); }
        }
    }

    clear = function () {
        for (i = scene.meshes.length - 1; i >= 0; i--) {
            var mesh = scene.meshes[i];
            if (mesh.name !== "skyBox") {
                //console.log("deleting mesh:" + mesh.name);
                mesh.dispose();
            }
        }
        cameraPositionSet = false;
    }

    drawSphere = function (vector, color, name) {
        vector = JSON.parse(vector);

        removeMeshes(name);
        var sphere = BABYLON.Mesh.CreateSphere(name, 10.0, 0.5, scene, false, BABYLON.Mesh.DEFAULTSIDE);
        var material = new BABYLON.StandardMaterial(scene);
        material.alpha = 1;
        material.diffuseColor = getColour(color);
        sphere.material = material;
        sphere.position = new BABYLON.Vector3(vector.x / div, (vector.z /div) + getHeight(color), vector.y / div);

        //console.log("drawSphere: " + name + " completed.");
    }

    drawLine = function (vector, color, name) {
        vector = JSON.parse(vector);

        //log("drawLine: " + name);

        removeMeshes(name);
        var line1 = [
            new BABYLON.Vector3(vector.x / div, vector.z / div, vector.y / div),
            new BABYLON.Vector3(vector.x / div, (vector.z / div) + 10, vector.y / div)];

        var lines1 = BABYLON.MeshBuilder.CreateLines(name, { points: line1 }, scene);
        lines1.color = getColour(color);

        if (/*!cameraPositionSet || */name === "start") {
            cameraPositionSet = false;

            setCamera(vector, vector, 10);
        }

        //console.log("drawLine: " + name + " completed.");
    }

    cameraPositionSet = false;
    startedRendering = false;
    modelId = 0;

    getColour = function(color)
    {
        switch (color)
        {
            case 1: return BABYLON.Color3.Red();
            case 2: return BABYLON.Color3.Green();
            case 3: return BABYLON.Color3.Blue();
            case 5: return BABYLON.Color3.Teal();
            case 6: return new BABYLON.Color3(1, 0.6, 0);
            case 4:
            default: return BABYLON.Color3.White();
        }
    }

    getHeight = function (color) {
        switch (color) {
            case 2: return 0.5;
            case 4: return 0.1;
            default: return 0.11;
        }
    }

    drawPath = function (points, color, name) {
        points = JSON.parse(points);

        //log("drawPath: " + name);

        const path = [];
        for (i = 0; i < points.length; i++) {
            const p = points[i];
            const height = getHeight(color);// === 4 ? 0.1 : 0.11;
            path.push(new BABYLON.Vector3(p.x / div, (p.z / div) + height, p.y / div));
        }

        const lines = BABYLON.MeshBuilder.CreateLines(name, { points: path }, scene);
        lines.color = getColour(color);

        setCamera(points[0], points[points.length - 1], 20);

        //console.log("drawPath: " + name + " completed.");
    }

    createScene = function () {
        log("createScene");

        canvas = document.getElementById('renderCanvas');// get the canvas DOM element
        engine = new BABYLON.Engine(canvas, true); // load the 3D engine

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

        var cameraMinSpeed = 0.1;
        var cameraMaxSpeed = 1;
        camera.speed = cameraMinSpeed;

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
            if (!startedRendering) {
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

        console.log("createScene: completed.");
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

        var positions = [];
        for (i = 0; i < loadedPositions.length; i++) {
            positions.push(loadedPositions[i].x / div);
            positions.push(loadedPositions[i].z / div);
            positions.push(loadedPositions[i].y / div);
        }

        //take uv value relative to bottom left corner of roof (-4, -4) noting length and width of roof is 8. base uv value on the x, z coordinates only
        var uvs = [];
        for (var p = 0; p < positions.length / 3; p++) {
            uvs.push((positions[3 * p] - (-4)) / 4, (positions[3 * p + 2] - (-4)) / 4);
        }

        var textures = ["grass.png", "waterbump.png", "floor.png", "ground.jpg"];

        // add the models
        for (var p = 0; p < loadedIndices.length; p++) {
            var indices = loadedIndices[p];

            modelId++;
            var customMesh = new BABYLON.Mesh("custom" + modelId, scene);
            var normals = [];
            BABYLON.VertexData.ComputeNormals(positions, indices, normals);
            var vertexData = new BABYLON.VertexData();
            vertexData.positions = positions;
            vertexData.indices = indices;
            vertexData.normals = normals;
            vertexData.uvs = uvs;
            vertexData.applyToMesh(customMesh);
            //customMesh.convertToFlatShadedMesh();

            var mat1 = new BABYLON.StandardMaterial("mat" + modelId, scene);

            mat1.diffuseTexture = new BABYLON.Texture("https://www.babylonjs-playground.com/textures/" + textures[p])
            //mat.wireframe = true;
            mat1.backFaceCulling = false;
            customMesh.material = mat1;
        }

        if (!startedRendering) {
            startedRendering = true;
            // run the render loop
            engine.runRenderLoop(function () {
                scene.render();
            });
        }

        //console.log("addModels completed");
    }

    setCamera = function (pos, look, height) {
        if (height === undefined) {
            height = 0;
        }
        const camera = scene.activeCamera;
        if (!cameraPositionSet) {
            cameraPositionSet = true;
            camera.position = new BABYLON.Vector3(pos.x / div, (pos.z / div) + height, pos.y / div);
            camera.setTarget(new BABYLON.Vector3(look.x / div, look.z / div, look.y / div));
        }
    }
});