# Morph documentation (VitePress)

Source for the public Morph docs site.

## Local

```bash
npm ci
npm run dev      # http://127.0.0.1:5180
npm run build
```

## Vercel

Create a Vercel project with:

| Setting | Value |
|---------|--------|
| Root Directory | `apps/docs` |
| Install Command | `npm ci` |
| Build Command | `npm run build` |
| Output Directory | `.vitepress/dist` |

`vercel.json` in this folder encodes the same settings for CLI deploys:

```bash
cd apps/docs
npx vercel --prod
```
