---
name: git-flow
description: Reglas de commit, rama y PR de Tiffany. Usar antes de cada commit, push o `gh pr create`.
---

# Git en Tiffany

## Commits

- Título en inglés, imperativo, 72 caracteres o menos. Cuerpo en español, cinco líneas o menos, con el porqué.
- Los commits no llevan firmas de herramientas: nada de `Co-Authored-By`, `Generated with` ni enlaces a asistentes. La declaración de IA vive en `USO-DE-IA.md`.
- Se agregan rutas explícitas. Nunca `git add .` ni `git add -A` en la raíz.
- Nunca `--no-verify` ni `--amend` después de push.
- Antes del commit, busca secretos en lo staged: `git diff --cached | grep -iE "api[_-]?key|secret|token|password"`. Las claves viven fuera del repo.
- Mensaje por heredoc y comprobación con `git log -1`.

## Ramas y PRs

- Base siempre `main`, que está protegida. Rama `<tipo>/<tema>`: `feat`, `fix`, `refactor`, `art`, `data`, `docs`, `chore`.
- Push solo de la propia rama.
- Antes del PR: `clean-comments`, tests en verde y la rama rebasada sobre `origin/main`.
- `gh pr create --base main` con cuerpo en español: Por qué, Qué cambia, Cómo probarlo.
- PR de 200 a 300 líneas como máximo. Un refactor va aparte de la funcionalidad.
- Se mergea con squash después de la revisión.
- Si dos ramas tocaron `Assets/Scenes/Main.unity`, en el rebase se toma la de `main` y se regenera con el builder.
