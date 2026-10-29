import bpy


def linear_rgba(color):
    color = color.lstrip("#")
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in rgb) + (1,)


def make_materials(entries):
    for entry in entries:
        material = bpy.data.materials.new(entry["name"])
        if hasattr(material, "use_nodes") and not material.use_nodes:
            material.use_nodes = True
        material.diffuse_color = linear_rgba(entry["color"])
        nodes, links = material.node_tree.nodes, material.node_tree.links
        shader = nodes["Principled BSDF"]
        shader.inputs["Base Color"].default_value = material.diffuse_color
        shader.inputs["Roughness"].default_value = 1 - entry["smoothness"]
        if entry.get("spirit", False):
            facing = nodes.new("ShaderNodeLayerWeight")
            facing.inputs["Blend"].default_value = 0.5
            power = nodes.new("ShaderNodeMath")
            power.operation = "POWER"
            power.inputs[1].default_value = entry.get("rimPower", 3)
            links.new(facing.outputs["Facing"], power.inputs[0])
            strength = nodes.new("ShaderNodeMath")
            strength.operation = "MULTIPLY"
            strength.inputs[1].default_value = entry.get("rimIntensity", 1.5)
            links.new(power.outputs[0], strength.inputs[0])
            shader.inputs["Emission Color"].default_value = linear_rgba(entry.get("rimColor", "#BFE3FF"))
            links.new(strength.outputs[0], shader.inputs["Emission Strength"])
