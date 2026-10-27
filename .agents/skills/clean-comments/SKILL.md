---
name: clean-comments
description: Borra y recorta los comentarios que sobran en un diff antes de abrir un PR. Se aplica, no se reporta. Usar antes de cada commit o PR, o cuando alguien pida "limpia los comentarios". Acepta un scope opcional (staged, una ruta, o --base <ref> para una rama ya commiteada).
---

# Clean Comments

Los comentarios documentan un constraint del código, nunca cómo se descubrió. Esta pasada borra y recorta los que sobran en el diff y al final dice qué hizo.

## Alcance

Solo comentarios agregados en el diff. Un comentario que ya estaba en el repo no se toca.

| Argumento | Scope |
| --- | --- |
| *(ninguno)* | todo lo no commiteado, incluidos archivos nuevos |
| `staged` | solo lo staged |
| `<ruta>` | ese archivo o directorio |
| `--base <ref>` | lo commiteado desde `<ref>`, con el árbol limpio |

## 1. Snapshot y extracción

```bash
export GIT_INDEX_FILE=$(mktemp -u)
git read-tree HEAD && git add -A && SNAP=$(git write-tree)
unset GIT_INDEX_FILE

git diff HEAD -U0 -- . ':(exclude)*.md' | grep -E '^\+\s*(//|#|/\*|\*[^/])' | sed 's/^+//'
git ls-files --others --exclude-standard | grep -vE '\.(md|json|ya?ml|meta|unity|asset|prefab|mat)$' | xargs grep -nE '^\s*(//|#|/\*|\*[^/])' 2>/dev/null
```

Con `--base <ref>`: `SNAP=HEAD` y la extracción es `git diff <ref>...HEAD -U0 | grep -E '^\+\s*(//|#|/\*|\*[^/])'`.

Cada comentario se juzga aislado, sin el código alrededor. Leído en su contexto todo comentario parece justificado.

## 2. Criterios

El primero que aplique decide.

| Señal | Acción |
| --- | --- |
| Narra una consecuencia ("esto causaría", "sin esto pasaría") | Borrar esa cláusula |
| Habla del cambio o de quien revisa ("ahora hacemos", "antes esto era") | Borrar entero |
| Repite lo que el código ya dice | Borrar entero |
| Repite un constraint escrito en otro archivo | Borrar entero |
| Cita docs internos, planes, issues o tareas | Borrar entero |
| Separador de sección en un archivo nuevo | Borrar entero |
| Sobrevive a todo lo anterior | Recortar a una línea |

Ningún comentario pasa de una línea, salvo dos constraints independientes.

## 3. Límite

Solo se borran o acortan comentarios. Ni una línea de código ejecutable cambia. Un bug que aparezca al limpiar se reporta, no se arregla aquí.

## 4. Verificar

```bash
export GIT_INDEX_FILE=$(mktemp -u)
git read-tree HEAD && git add -A && NOW=$(git write-tree)
unset GIT_INDEX_FILE
git diff $SNAP $NOW -U0 | grep -E '^[+-]' | grep -vE '^[+-]{3}' | grep -vE '^[+-]\s*(//|#|/\*|\*[^/])'
```

La salida debe quedar vacía. Si no, se revierte el archivo con `git checkout $SNAP -- <archivo>` y se rehace.

Cierra con una línea: cuántos comentarios se borraron, cuántos se recortaron y cuántos quedaron.
