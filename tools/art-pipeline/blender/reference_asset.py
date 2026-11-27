"""Procedural reference machine shared by render.py (HD passes) and export_mesh.py (mesh-export route). No external file."""
import bpy


def _toon(name, color):
    m = bpy.data.materials["toon-base"].copy()
    m.name = name
    m.node_tree.nodes["Toon BSDF"].inputs["Color"].default_value = color
    return m


def _emit(name, color, strength):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes.clear()
    em = m.node_tree.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = color
    em.inputs["Strength"].default_value = strength
    out = m.node_tree.nodes.new("ShaderNodeOutputMaterial")
    m.node_tree.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return m


def build_reference_machine(scene):
    """Procedural reference subject: a body box, a chimney and an emissive vent strip. Footprint 1.6 x 1.2 world units,
    centred on the origin, resting on z = 0. No external file, so the reproducibility test needs no binary asset."""
    body_mat = _toon("body-ref", (0.30, 0.36, 0.42, 1.0))
    trim_mat = _toon("trim-ref", (0.55, 0.40, 0.25, 1.0))
    emit_mat = _emit("emit-vent", (1.0, 0.62, 0.2, 1.0), 1.0)
    objs = []
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.45))
    body = bpy.context.object
    body.name = "ref-body"
    body.scale = (1.6, 1.2, 0.9)
    body.data.materials.append(body_mat)
    objs.append(body)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.18, depth=0.7, location=(-0.45, 0.25, 1.25))
    chim = bpy.context.object
    chim.name = "ref-chimney"
    chim.data.materials.append(trim_mat)
    objs.append(chim)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.2, -0.61, 0.5))
    vent = bpy.context.object
    vent.name = "ref-vent"
    vent.scale = (0.8, 0.04, 0.22)
    vent.data.materials.append(emit_mat)
    objs.append(vent)
    return objs


