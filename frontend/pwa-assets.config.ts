import { defineConfig, minimal2023Preset } from '@vite-pwa/assets-generator/config'

export default defineConfig({
  headLinkOptions: { preset: '2023' },
  preset: {
    ...minimal2023Preset,
    maskable: { sizes: [192, 512] },
    apple: { sizes: [180] },
  },
  images: ['public/pwa-source.svg'],
})
