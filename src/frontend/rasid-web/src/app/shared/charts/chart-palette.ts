/**
 * Chart colours per theme. Canvas charts cannot read CSS light-dark() values, so the same brand
 * palette is expressed here for both themes.
 */
export interface ChartPalette {
  text: string;
  muted: string;
  grid: string;
  surface: string;
  brand: string;
  brandFill: string;
  accent: string;
  breakdown: [string, string, string, string];
  levels: [string, string, string, string];
}

const Light: ChartPalette = {
  text: '#0f1c26',
  muted: '#5a6b7a',
  grid: 'rgba(15, 28, 38, 0.08)',
  surface: '#ffffff',
  brand: '#0f8f86',
  brandFill: 'rgba(15, 143, 134, 0.18)',
  accent: '#d9a23b',
  breakdown: ['#0f8f86', '#2563eb', '#d9a23b', '#8b5cf6'],
  levels: ['#0f9d6b', '#c98a12', '#5b7083', '#a0aab4'],
};

const Dark: ChartPalette = {
  text: '#e7eef4',
  muted: '#93a5b6',
  grid: 'rgba(231, 238, 244, 0.08)',
  surface: '#121a22',
  brand: '#2dd4bf',
  brandFill: 'rgba(45, 212, 191, 0.18)',
  accent: '#fbbf24',
  breakdown: ['#2dd4bf', '#60a5fa', '#fbbf24', '#a78bfa'],
  levels: ['#34d399', '#fbbf24', '#94a3b8', '#4b5a69'],
};

export function chartPalette(dark: boolean): ChartPalette {
  return dark ? Dark : Light;
}
