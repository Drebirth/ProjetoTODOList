---
name: "Foco — TODO Design System"
description: "Sistema de design limpo e produtivo para aplicativo de tarefas (TODO List), combinando neutros sofisticados (branco/cinza) com terracota e vermelho caloroso."
---

## Overview

O Foco é um sistema de design criado para produtividade pessoal e colaborativa em listas de tarefas. Focado em redução de atrito mental, alia clareza tipográfica, superfícies neutras de alto contraste e acentos quentes em tons de terracota e vermelho para sinalizar foco, prioridade e ações de destaque. O estilo mistura minimalismo moderno e toques táteis sutis com cantos levemente arredondados e elevações suaves.

## Colors

A paleta combina o calor do terracota e vermelho terroso como destaque (`primary`), complementado por neutros puros e funcionais (branco e cinzas balanceados) para descanso visual e máxima legibilidade.

- **Primary (Terracota):** Cor de ação e foco, quente e enérgica sem agredir os olhos.
- **Secondary (Vermelho Queimado):** Usado para alertas de prazo crítico e alta prioridade.
- **Surface & Background:** Branco e tons neutros de cinza com alto contraste para organizar blocos de tarefas.

```yaml
colors:
  primary: "#C05621"
  on-primary: "#FFFFFF"
  primary-container: "#FEEBC8"
  on-primary-container: "#7B341E"
  secondary: "#C53030"
  on-secondary: "#FFFFFF"
  secondary-container: "#FED7D7"
  on-secondary-container: "#742A2A"
  terracota: "#C05621"
  vermelho: "#C53030"
  success: "#2F855A"
  on-success: "#FFFFFF"
  warning: "#DD6B20"
  on-warning: "#252525"
  light:
    surface: "#FFFFFF"
    on-surface: "#1A202C"
    surface-variant: "#F7FAFC"
    on-surface-variant: "#4A5568"
    outline: "#E2E8F0"
    outline-focus: "#CBD5E0"
  dark:
    surface: "#1A202C"
    on-surface: "#F7FAFC"
    surface-variant: "#2D3748"
    on-surface-variant: "#E2E8F0"
    outline: "#4A5568"
    outline-focus: "#718096"
```

## Typography

Utilizamos **Inter** para todas as escalas de texto, proporcionando excelente legibilidade tanto em telas de celular quanto em desktops, com ritmo vertical consistente.

```yaml
typography:
  display-lg:
    fontFamily: "Inter"
    fontSize: 40px
    fontWeight: 700
    lineHeight: 48px
    letterSpacing: -0.5px
  display-lg-mobile:
    fontFamily: "Inter"
    fontSize: 28px
    fontWeight: 700
    lineHeight: 34px
    letterSpacing: -0.3px
  headline-lg:
    fontFamily: "Inter"
    fontSize: 32px
    fontWeight: 700
    lineHeight: 40px
    letterSpacing: -0.4px
  headline-lg-mobile:
    fontFamily: "Inter"
    fontSize: 24px
    fontWeight: 700
    lineHeight: 30px
    letterSpacing: -0.2px
  headline-md:
    fontFamily: "Inter"
    fontSize: 24px
    fontWeight: 600
    lineHeight: 32px
    letterSpacing: -0.2px
  headline-md-mobile:
    fontFamily: "Inter"
    fontSize: 20px
    fontWeight: 600
    lineHeight: 26px
    letterSpacing: -0.1px
  title-lg:
    fontFamily: "Inter"
    fontSize: 18px
    fontWeight: 600
    lineHeight: 24px
  title-md:
    fontFamily: "Inter"
    fontSize: 16px
    fontWeight: 600
    lineHeight: 22px
  body-lg:
    fontFamily: "Inter"
    fontSize: 16px
    fontWeight: 400
    lineHeight: 24px
  body-md:
    fontFamily: "Inter"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 20px
  label-lg:
    fontFamily: "Inter"
    fontSize: 14px
    fontWeight: 600
    lineHeight: 20px
  label-md:
    fontFamily: "Inter"
    fontSize: 12px
    fontWeight: 500
    lineHeight: 16px
  label-sm:
    fontFamily: "Inter"
    fontSize: 11px
    fontWeight: 500
    lineHeight: 14px
```

## Layout & Spacing

Grade flexível e responsiva construída em passos de 8px (com meio-passo de 4px para chips e ícones). Em desktop, layout em duas colunas (barra lateral de navegação e filtros + lista central com drawer de detalhes). Em mobile, navegação inferior fluida, drawer retrátil e botão flutuante rápido para criar tarefas.

```yaml
spacing:
  gutter-xs: 4px
  gutter-sm: 8px
  gutter-md: 16px
  gutter-lg: 24px
  gutter-xl: 32px
  section: 40px
  page-margin: 16px
```

## Elevation & Depth

Utiliza elevação sutil com sombras difusas e neutras (`box-shadow`), combinadas com bordas de baixo contraste (`outline`) para separar blocos e cartões sem gerar sobrecarga visual.

## Shapes

Bordas arredondadas moderadas para criar sensação contemporânea e amigável.

```yaml
rounded:
  sm: 6px
  DEFAULT: 10px
  md: 12px
  lg: 16px
  xl: 20px
  full: 9999px
```

## Motion & Micro-interaction

Microinterações táteis: checkboxes com preenchimento elástico, itens concluídos com risco animado sutil e transições rápidas de entrada e saída.

```yaml
motion:
  state-change:
    duration: 150ms
    delay: 0ms
    timingFunction: ease-out
  surface-enter:
    duration: 250ms
    delay: 0ms
    timingFunction: cubic-bezier(0.16, 1, 0.3, 1)
  surface-exit:
    duration: 180ms
    delay: 0ms
    timingFunction: cubic-bezier(0.4, 0, 1, 1)
```

## Voice & Content Strategy

Comunicação clara, objetiva e motivadora em português. Verbos no infinitivo ou imperativo direto nas ações ("Criar tarefa", "Concluir", "Filtrar"). Contadores de progresso visíveis e incentivo positivo ao zerar a lista do dia.

## Components

Component implementations live as individual HTML sticker sheet files in
`components/`. Each file contains the canonical, styled HTML components with
`<!-- COMPONENT: name -->` comment markers. Read each file before use.

- **Badge** → `@components/badge.html`: `badge-numeric`, `badge-dot`
- **Button** → `@components/button.html`: `button-primary`, `button-icon`, `button-with-icon`
- **Card** → `@components/card.html`: `card-elevated`, `card-action`, `card-interactive`, `card-interactive-2`
- **Input Field** → `@components/input-field.html`: `input-field-text`, `input-field-icon`
- **Tabs** → `@components/tabs.html`: `tabs-horizontal`, `tabs-with-icon`
