# Repairer model import

The editable body and arm are separate rigid meshes. The body origin is on the ground. The arm origin is the shoulder, so rotating the arm leaves the chassis still. Both objects have unit scale and zero Euler rotation in Blender. The Unity prefab rotates the model root 180 degrees around Y for the gameplay forward direction.

| Mesh | Blender vertices | Blender faces | UV maps | Local dimensions in metres |
| --- | ---: | ---: | ---: | --- |
| Repairer_Body | 840 | 906 | 1 | 0.754 × 0.793 × 1.100 |
| Repairer_Arm | 424 | 422 | 1 | 0.304 × 0.535 × 0.296 |

Reimporting the FBX in Blender gives the same mesh dimensions. The FBX material names map to four URP materials: petrol enamel, graphite, amber and mint optics. The design uses solid colours. The UV map is available for later texturing.

Unity keeps the node hierarchy for the shoulder and the four external material mappings. Lights, cameras, visibility tracks and blend shapes are disabled because neither source mesh needs them. The optics material carries its own mint emission so the eyes stay distinguishable from the amber case. Gameplay uses the existing Body collider and root NavMeshAgent independently of the visual mesh.
