Special Forces: building the asset bundles (one time, after every model/map change)

1. Unity 2021.3.45f2 exactly (Unity Hub may pick Unity 6: the game rejects those bundles). New 3D (Built-in) project.
2. Make the folder Assets/SpecialForces/ and copy all eight .fbx files from this folder into it.
3. Make the folder Assets/Editor/ and copy SpecialForcesBuilder.cs into it.
4. Menu: Special Forces > Build Asset Bundles. When it finishes it shows where the two files are.
5. Copy  <project>/AssetBundles/sfstormvalley  and  <project>/AssetBundles/specialforcesassets
   next to SpecialForces.dll  (BepInEx/plugins/<...>/SpecialForces/).
The map FBX is big (~17 MB, 1 km of terrain); the first import takes a minute.
