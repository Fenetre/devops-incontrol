/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{vue,js}'],
  theme: {
    extend: {
      colors: {
        primary: {
          50: '#eef9fe',
          100: '#d8f1fd',
          200: '#b0e4fb',
          300: '#78d2f8',
          400: '#38bdf2',
          500: '#00aeef',
          600: '#008dcc',
          700: '#0071a6',
          800: '#005e8c',
          900: '#004d73',
        },
        sidebar: '#262660',
        'sidebar-light': '#00aeef',
      },
    },
  },
  plugins: [],
}
