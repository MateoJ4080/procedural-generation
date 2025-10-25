# Procedural-Generation

## Terrain Parameters 

Each parameter information will be splitted in two sections: 
- What they are
- How their purpose is achieved through code - _(Not done in README yet)_
<!-- Might want to add comparative images later, ideally when textures are assigned to the terrain -->

### Frequency

Controls how close or far apart the hills and valleys are.
* Bigger number → hills and valleys are closer together, terrain is “spikier.”
* Smaller number → hills and valleys are farther apart, terrain is flatter and smoother.

### Amplitude

Controls how tall the hills and how deep the valleys are.
* Bigger number → taller hills, deeper valleys.
* Smaller number → shorter hills, shallower valleys.

### Octaves

Controls how many layers of noise are combined.
* More octaves → more detail in the terrain.
* Fewer octaves → simpler, smoother terrain.

### Persistence

Controls how much each additional layer affects the terrain.
* Smaller number → upper layers contribute less, smoother terrain.
* Bigger number → upper layers contribute more, rougher terrain.

### Lacunarity

Controls how much the detail increases for each layer.
* Bigger number → higher layers have finer, faster-changing details.
* Smaller number → higher layers add less fine detail.
