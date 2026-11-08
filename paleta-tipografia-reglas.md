# Paleta oscura sobria

Sistema de color para una web moderna y minimalista en modo oscuro. Parte de tres colores base y los extiende en fondos, texto, estados y semánticos.

| Rol | Color base |
|---|---|
| Marca | `#ffde59` |
| Secundario | `#8b7cf6` |
| Background | `#0a0d17` |

> Ajuste respecto al mensaje anterior: el texto apagado pasa de `#6b7189` a `#80869e`, para que cumpla contraste AA (4.5:1) sobre las tres superficies.

---

## 1. Fondos y bordes

Neutros con tinte azul-violeta, nunca grises planos. Cada escalón sube la luminosidad poco a poco, así la profundidad se lee por capas y no por sombras.

| Token | Hex | Uso |
|---|---|---|
| `--bg` | `#0a0d17` | Fondo de página |
| `--surface` | `#10141f` | Cards, paneles |
| `--elevated` | `#171c2a` | Modales, menús, popovers |
| `--hover` | `#1f2536` | Hover de filas y elementos |
| `--border` | `#252b3d` | Bordes sutiles (1px) |
| `--border-strong` | `#343c54` | Inputs, separadores marcados |

## 2. Texto

| Token | Hex | Contraste sobre `--bg` | Uso |
|---|---|---|---|
| `--text` | `#eceef5` | 16.7:1 | Texto principal (evita el blanco puro) |
| `--text-2` | `#a4a9bd` | 8.3:1 | Párrafos secundarios, descripciones |
| `--muted` | `#80869e` | 5.4:1 | Metadatos, captions, placeholders |
| `--disabled` | `#444a60` | 2.2:1 | Solo elementos deshabilitados |

## 3. Marca (amarillo)

| Token | Hex | Uso |
|---|---|---|
| `--brand` | `#ffde59` | CTA principal, acento tipográfico, indicador activo |
| `--brand-hover` | `#ffe883` | Hover |
| `--brand-pressed` | `#e6c53f` | Pressed |
| `--brand-tint` | `#ffde591a` | Fondo de badges (10 %) |
| `--on-brand` | `#0a0d17` | Texto sobre amarillo (14.6:1) |

## 4. Secundario (violeta)

| Token | Hex | Uso |
|---|---|---|
| `--accent` | `#8b7cf6` | Links, focus rings, estados secundarios |
| `--accent-hover` | `#a094f9` | Hover de links |
| `--accent-pressed` | `#7060e0` | Pressed |
| `--accent-tint` | `#8b7cf61f` | Selección y fondo de focus |
| `--accent-deep` | `#2a2557` | Fondos suaves, bloques de énfasis |

## 5. Semánticos

Desaturados a propósito, para que no compitan con el amarillo de marca.

| Token | Hex | Uso |
|---|---|---|
| `--success` | `#5fc9a0` | Éxito, confirmaciones |
| `--warning` | `#f0a45d` | Avisos |
| `--danger` | `#ef6b7b` | Errores, acciones destructivas |
| `--info` | `#6cb4f0` | Información neutra |

---

## 6. Cómo evitar el look genérico

1. **Amarillo escaso.** Menos del 5 % de la pantalla: el CTA principal, un acento tipográfico, el indicador activo. Funciona como señal, no como decoración.
2. **Violeta como apoyo.** Reservado para links, focus rings, selección y estados secundarios. Nada de degradados violeta-rosa.
3. **Profundidad por capas.** Separa superficies con bordes de 1px y escalones de fondo. Evita sombras pesadas y el glassmorphism.
4. **Blanco roto.** Usa `#eceef5` para el texto principal; el blanco puro sobre un fondo tan oscuro cansa la vista.
5. **Una sola tipografía.** Radio Canada en toda la interfaz. El contraste sale del peso y el tamaño, no de mezclar familias (ver sección 8).
6. **Un solo gesto audaz.** Concentra el color en un punto por pantalla y deja el resto en neutros.
7. **Estados claros.** Hover con un escalón de fondo (`--hover`), focus con anillo violeta de 2px, nunca solo un cambio de color de texto.

## 7. Accesibilidad

Ratios calculados con la fórmula WCAG 2.x.

| Combinación | `--bg` | `--surface` | `--elevated` |
|---|---|---|---|
| `--text` | 16.7 | 15.9 | 14.7 |
| `--text-2` | 8.3 | 7.9 | 7.3 |
| `--muted` | 5.4 | 5.1 | 4.7 |
| `--brand` | 14.6 | 13.9 | 12.8 |
| `--accent` | 5.8 | 5.5 | 5.1 |
| `--accent-hover` | 7.5 | 7.1 | 6.6 |
| `--success` | 9.6 | 9.1 | 8.4 |
| `--warning` | 9.4 | 8.9 | 8.2 |
| `--danger` | 6.5 | 6.2 | 5.7 |
| `--info` | 8.7 | 8.3 | 7.6 |

- Todo el texto de la tabla supera AA (4.5:1) en las tres superficies.
- `--on-brand` (`#0a0d17`) sobre `--brand` da 14.6:1; sobre `--accent` da 5.8:1. Un botón violeta lleva texto oscuro, no blanco (blanco sobre `#8b7cf6` solo da 3.3:1).
- `--border` sobre `--bg` da 1.4:1: es decorativo. Los bordes de inputs usan `--border-strong` (1.8:1) y un anillo de focus violeta para cumplir con los componentes interactivos.
- `--disabled` no cumple contraste, y es lo esperado en elementos deshabilitados.

## 8. Tipografía

Familia única: **Radio Canada** (Google Fonts, pesos 400 a 700).

| Papel | Peso | Ajustes |
|---|---|---|
| Titulares | 700 | `letter-spacing: -0.03em`, `line-height: 1.0–1.1`, `text-wrap: balance` |
| Texto corrido | 400 | `line-height: 1.6`, ancho máximo de 60–65 caracteres |
| Botones y formularios | 500–600 | Tamaño 14–15px |
| Etiquetas y metadatos | 500 | Mayúsculas, `letter-spacing: 0.12em`, 12px |
| Datos y cifras | 500 | `font-variant-numeric: tabular-nums` |

```html
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Radio+Canada:wght@400;500;600;700&display=swap">
```

## 9. Formas

Esquinas suaves, nunca pastilla. Cuanto más grande el contenedor, más radio, y lo que va dentro lleva un radio menor que su contenedor (card de 12px, elementos internos de 8px).

| Token | Valor | Uso |
|---|---|---|
| `--r-sm` | 4px | Badges, chips, etiquetas |
| `--r-md` | 8px | Botones, inputs |
| `--r-lg` | 12px | Cards, paneles |
| `--r-xl` | 16px | Modales, contenedores grandes |
| (pastilla) | 999px | Excepción: un solo elemento, como un toggle |

**Iconos propios**
- Cuadrícula de 24×24 con margen de seguridad de 2px.
- Trazo de 1.5px con terminales y uniones redondeados.
- Esquinas exteriores con radio de 2px a 3px, el mismo en todo el set.
- Outline por defecto y fill para el estado activo, con la misma geometría en ambas variantes.
- SVG con `currentColor` y nombres `home` / `home-fill`.

## 10. Tokens CSS

```css
:root {
  color-scheme: dark;

  /* Tipografía */
  --font: "Radio Canada", system-ui, sans-serif;

  /* Fondos y bordes */
  --bg: #0a0d17;
  --surface: #10141f;
  --elevated: #171c2a;
  --hover: #1f2536;
  --border: #252b3d;
  --border-strong: #343c54;

  /* Texto */
  --text: #eceef5;
  --text-2: #a4a9bd;
  --muted: #80869e;
  --disabled: #444a60;

  /* Marca */
  --brand: #ffde59;
  --brand-hover: #ffe883;
  --brand-pressed: #e6c53f;
  --brand-tint: #ffde591a;
  --on-brand: #0a0d17;

  /* Secundario */
  --accent: #8b7cf6;
  --accent-hover: #a094f9;
  --accent-pressed: #7060e0;
  --accent-tint: #8b7cf61f;
  --accent-deep: #2a2557;

  /* Semánticos */
  --success: #5fc9a0;
  --warning: #f0a45d;
  --danger: #ef6b7b;
  --info: #6cb4f0;

  /* Formas */
  --r-sm: 4px;
  --r-md: 8px;
  --r-lg: 12px;
  --r-xl: 16px;
}
```

## 11. Tailwind (opcional)

```js
// tailwind.config.js
export default {
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Radio Canada"', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        sm: '4px',
        md: '8px',
        lg: '12px',
        xl: '16px',
      },
      colors: {
        bg: '#0a0d17',
        surface: '#10141f',
        elevated: '#171c2a',
        hover: '#1f2536',
        line: { DEFAULT: '#252b3d', strong: '#343c54' },
        ink: { DEFAULT: '#eceef5', 2: '#a4a9bd', muted: '#80869e', off: '#444a60' },
        brand: { DEFAULT: '#ffde59', hover: '#ffe883', pressed: '#e6c53f' },
        accent: { DEFAULT: '#8b7cf6', hover: '#a094f9', pressed: '#7060e0', deep: '#2a2557' },
        success: '#5fc9a0',
        warning: '#f0a45d',
        danger: '#ef6b7b',
        info: '#6cb4f0',
      },
    },
  },
}
```
