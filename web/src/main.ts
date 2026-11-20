import { createApp } from 'vue'
import App from './App.vue'
import { router } from './router'
import { vReveal, vSpotlight, vTilt } from './lib/effects'
import './styles/fonts.css'
import './styles/tokens.css'
import './styles/base.css'

const app = createApp(App)

app.use(router)
app.directive('reveal', vReveal)
app.directive('spotlight', vSpotlight)
app.directive('tilt', vTilt)
app.mount('#app')
