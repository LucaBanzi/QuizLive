import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        ink: '#12141c',        // fondo serata
        panel: '#1c2030',
        line: '#2d3350',
        paper: '#f4f2ec',
        mute: '#9aa1bd',
        chalk: '#ffd23f',      // accento: gesso giallo da lavagna
        ok: '#2fbf71',
        bad: '#ef4a53',
        ansA: '#ef4a53',
        ansB: '#3d7bff',
        ansC: '#ffb020',
        ansD: '#22b573',
      },
      fontFamily: {
        sans: ['"Trebuchet MS"', '"Segoe UI"', 'system-ui', '-apple-system', 'Roboto', 'sans-serif'],
      },
    },
  },
  plugins: [],
} satisfies Config;
