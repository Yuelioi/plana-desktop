const element = document.querySelector('#player')
let player = null
let idle = ''
let animations = []
let pointer = null
let clickTimer = 0

function post(message) { window.chrome?.webview?.postMessage(message) }
function randomAnimation() {
  const preferred = animations.filter(name => name !== idle && /pat|look|touch|tap|talk|^s_/i.test(name))
  const fallback = animations.filter(name => name !== idle && !/setup|setting|left|right/i.test(name))
  const candidates = preferred.length ? preferred : fallback
  return candidates[Math.floor(Math.random() * candidates.length)] || idle
}
function playAnimation(name) {
  if (!player) return
  const selected = name === 'random' ? randomAnimation() : name
  if (!selected) return
  player.animationState.setAnimation(0, selected, false)
  if (idle) player.animationState.addAnimation(0, idle, true, 0)
}
window.plana = { playAnimation }

element.addEventListener('pointerdown', event => {
  if (event.button !== 0) return
  pointer = { x: event.clientX, y: event.clientY }
  element.classList.add('dragging')
})
element.addEventListener('pointermove', event => {
  if (!pointer || Math.hypot(event.clientX - pointer.x, event.clientY - pointer.y) < 6) return
  pointer = null
  post({ type: 'drag' })
})
element.addEventListener('pointerup', () => {
  element.classList.remove('dragging')
  if (!pointer) return
  pointer = null
  window.clearTimeout(clickTimer)
  clickTimer = window.setTimeout(() => post({ type: 'interaction', interaction: 'click' }), 220)
})
element.addEventListener('dblclick', () => {
  window.clearTimeout(clickTimer)
  post({ type: 'interaction', interaction: 'doubleClick' })
})
element.addEventListener('contextmenu', event => {
  event.preventDefault()
  post({ type: 'context' })
})
element.addEventListener('pointercancel', () => { pointer = null; element.classList.remove('dragging') })

player = new spine.SpinePlayer('player', {
  skelUrl: '/spine/plana/NP0035_spr.skel',
  atlasUrl: '/spine/plana/NP0035_spr.atlas',
  showControls: false,
  premultipliedAlpha: false,
  backgroundColor: '#00000000',
  alpha: true,
  viewport: { autoSize: true, padLeft: '1%', padRight: '1%', padTop: '1%', padBottom: '1%' },
  success(instance) {
    player = instance
    animations = instance.skeleton?.data?.animations?.map(animation => animation.name) || []
    idle = animations.find(name => name === 'Idle_01') || animations.find(name => /idle/i.test(name)) || animations[0] || ''
    if (idle) instance.animationState.setAnimation(0, idle, true)
    post({ type: 'ready', animations })
  },
})
