Hey! If wou want to update FMOD Lib, copy these files.

These libs to DLL\WIN32
 - C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\core\lib\x64\fmodL.dll
 - C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\studio\lib\x64\fmodstudioL.dll

These auto-generated(!) C# files to Fmod (this dir). You need to change `VERSION.dll` const to relative path to fmod(L) lib, i.e. "DLL/WIN32/fmodL".
 - C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\studio\inc\fmod_studio.cs
 - C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\core\inc\fmod_errors.cs
 - C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\core\inc\fmod.cs
 - C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\core\inc\fmod_dsp.cs