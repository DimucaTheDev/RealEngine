Hi! A small explanation for what these folder are.


=FOLDERS=

Audio   - Store .wav files there, and dont forget to run "GenerateSoundMap.cs" script
Cfg     - Config files a.k.a. scripts. You can run them by using "source Assets/Cfg/FILE_NAME.cfg"
Fonts   - TTF fonts... meh.
Maps    - Store maps there in this path: Assets/Maps/[map_name]/data.json  . Check out existing maps to see how they work.
Models  - FBX model files are preffered. There is a script called "GenerateStaticModel.cs", which can convert
	     model files to .SMDL format. Kinda lightweight format for static models. For less bugs place textures
	     near your models. Animations is in WIP(02 september 2025)
Shaders - These shaders are supported: *.frag for FRAGMENT shaders, *.vert for VERTICES shaders, *.geom for 
	     GEOMETRY shaders. Those are GLSL shaders, OpenGL 4.6. dont use Cyrrillic symbols cuz its buggy.
Skybox  - Skybox is a big cube with textures. Textures are stored and loaded by these names:
		-  Assets/Skybox/[skybox_name]/back.png
		-  Assets/Skybox/[skybox_name]/bottom.png
		-  Assets/Skybox/[skybox_name]/front.png
		-  Assets/Skybox/[skybox_name]/left.png
		-  Assets/Skybox/[skybox_name]/right.png
		-  Assets/Skybox/[skybox_name]/top.png
Sprites - Small PNG images, 2D sprites. These are used in Scene Editor, for example.
Testing - I use this for dev purposes when I create new components...


=SCRIPTS=

There are some C# scripts in this folder. You can run then by using "dotnet SCRIPT_NAME.cs some_args". Please, note that
you have to use .NET 10+ SDK to run these. You can see what they do by opening them in code editors/notepad.

GenerateSoundMap.cs    - This script generates soundmap.json from files in Audio folder. This file is loaded by
			    Engine when FMOD is being initialized. Files named like "abc/name1.wav" and "abc/name2.wav"
			    will be groupped and one of them will be played on "abc/name".
GenerateStaticModel.cs - This script converts model(FBX or other format supported by Assimp lib) to .SMDL format.
			    It's lightweight binary format. You can check this script and source code to see how it works.

=ARGUMENTS=

-log path_to_file.txt    Engine will use Serilog template written in this file. Note that this templates only
                             applies to system console sink, not the game log you see in ingame console(~)
-s                       Uses Sequential task scheduler for Bullet physics engine.
-mpt                     Uses Multi-Processing (OpenMP) task scheduler for Bullet physics engine.
-tbb                     Uses Intel Threading Building Blocks task scheduler for Bullet physics engine.