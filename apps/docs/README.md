# Morph documentation (VitePress)

Source for the public Morph docs site.

**Live:** https://morph-docs-pink.vercel.app

## Local

```bash
npm ci
npm run dev      # http://127.0.0.1:5180
npm run build
```

## Vercel

Production project: `parhammoasgharis-projects/morph-docs` (Root Directory = this folder).

| Setting | Value |
|---------|--------|
| Root Directory | `apps/docs` |
| Install Command | `npm ci` / `npm install` |
| Build Command | `npm run build` |
| Output Directory | `.vitepress/dist` |

```bash
cd apps/docs
npx vercel --prod --scope parhammoasgharis-projects
```

Connect GitHub (`vercel git connect`) for deploys on every push to `main`.
