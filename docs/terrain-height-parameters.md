## Terrain Height Parameters

### This document explains how [ChunkHeightJob](../Assets/Scripts/Terrain/Jobs/ChunkHeightJob.cs) generates terrain height and how each parameter affects the final terrain.

Each parameter information will be splitted in two sections, marked by their respective emoji: 
- What it does 💡
- How its purpose is achieved through code 🎯
<!-- Might want to add comparative images later, probably of the textures they are assigned to the terrain. Also changing how 🎯 is structured and adding more detail -->

> **Note:** Downloading the project and experimenting with these values in the inspector while automatically regenerating the terrain at runtime will make these concepts easier to understand.

---

## Frequency

💡 Controls how close or far apart the hills and valleys are.
* Bigger number → hills and valleys are closer together, terrain is “spikier.”
* Smaller number → hills and valleys are farther apart, terrain is flatter and smoother.

🎯 Think of snoise like a texture where we are using its coordinates instead of a mathematical function giving us a value. By multiplying the coordinates with 'Frequency', 
you are just moving across that picture faster or slower. The function always returns values between -1 and 1, but scaling the input with this variable changes how quickly those values are reached.  

## Amplitude

💡 Controls how tall the hills and how deep the valleys are.
* Bigger number → taller hills, deeper valleys.
* Smaller number → shorter hills, shallower valleys.

🎯 Multiplies what value the noise function returns, increasing or decreasing the height difference between each coordinate but making it consistent at the same time.

## Octaves

💡 Controls how many layers of noise are combined.
* More octaves → more detail in the terrain.
* Fewer octaves → simpler, smoother terrain.

🎯 Octaves can be seen as layers of noise, each iteration over int octaves being a new layer added. 
'octaveFreq' defines how fast the noise changes in that layer by scaling the input coordinates. Higher values make the noise vary more rapidly, adding smaller details on top of the previous layers.

## Persistence

💡 Controls how much each additional layer affects the terrain.
* Smaller number → upper layers contribute less, smoother terrain.
* Bigger number → upper layers contribute more, rougher terrain.

🎯 Modifies amp each octave by multiplying it, which can either decrease or increase how much each new noise layer contributes.

## Lacunarity

💡 Controls how much the detail increases for each layer.
* Bigger number → higher layers have finer, faster-changing details.
* Smaller number → higher layers add less fine detail.

🎯 Each octave multiplies the input coordinates (nx and nz) by octaveFreq, which itself is multiplied by Lacunarity after every iteration. This makes each new layer of noise sample from coordinates that are farther apart in the noise space, causing faster changes in values and thus adding finer details to the terrain.
