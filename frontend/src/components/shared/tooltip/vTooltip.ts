import type { Directive, DirectiveBinding } from 'vue'

type Placement = 'top' | 'bottom' | 'left' | 'right'
interface Options { text: string; placement: Placement; delay: number; disabled: boolean }
export type TooltipValue = string | Partial<Options>

interface State { opts: Options; tip?: HTMLDivElement; timer?: number; cleanup: () => void }

const GAP = 8
const states = new WeakMap<HTMLElement, State>()
const flip: Record<Placement, Placement> = { top: 'bottom', bottom: 'top', left: 'right', right: 'left' }

const parse = ({ value, arg }: DirectiveBinding<TooltipValue, string>): Options => ({
    text: '', placement: 'top', delay: 67, disabled: false,
    ...(typeof value === 'object' ? value : { text: value ?? '' }),
    ...(arg ? { placement: arg as Placement } : {}),
})

function coords(p: Placement, t: DOMRect, r: DOMRect) {
    const cx = t.left + (t.width - r.width) / 2, cy = t.top + (t.height - r.height) / 2
    return {
        top: { x: cx, y: t.top - r.height - GAP },
        bottom: { x: cx, y: t.bottom + GAP },
        left: { x: t.left - r.width - GAP, y: cy },
        right: { x: t.right + GAP, y: cy },
    }[p]
}

function place(el: HTMLElement, { tip, opts }: State) {
    if (!tip) return
    const t = el.getBoundingClientRect(), r = tip.getBoundingClientRect()
    const W = innerWidth, H = innerHeight
    const ok = ({ x, y }: { x: number; y: number }) => x >= 0 && y >= 0 && x + r.width <= W && y + r.height <= H
    let pos = coords(opts.placement, t, r)
    if (!ok(pos)) { const alt = coords(flip[opts.placement], t, r); if (ok(alt)) pos = alt }
    tip.style.left = `${Math.min(Math.max(pos.x, 4), W - r.width - 4)}px`
    tip.style.top = `${Math.min(Math.max(pos.y, 4), H - r.height - 4)}px`
}

function hide(el: HTMLElement, s: State) {
    clearTimeout(s.timer)
    s.tip?.remove()
    s.tip = undefined
    el.removeAttribute('aria-describedby')
}

function show(el: HTMLElement, s: State) {
    if (s.opts.disabled || !s.opts.text) return
    clearTimeout(s.timer)
    s.timer = window.setTimeout(() => {
        s.tip ??= Object.assign(document.createElement('div'), {
            className: 'v-tooltip', role: 'tooltip', id: `v-tooltip-${Math.random().toString(36).slice(2)}`,
        })
        s.tip.textContent = s.opts.text
        document.body.appendChild(s.tip)
        el.setAttribute('aria-describedby', s.tip.id)
        place(el, s)
    }, s.opts.delay)
}

export const vTooltip: Directive<HTMLElement, TooltipValue, string> = {
    mounted(el, binding) {
        const s: State = { opts: parse(binding), cleanup: () => { } }
        const ctrl = new AbortController()
        const on = (t: EventTarget, ev: string, fn: (e: any) => void, capture = false) =>
            t.addEventListener(ev, fn, { signal: ctrl.signal, passive: true, capture })

        const open = () => show(el, s), close = () => hide(el, s), reposition = () => place(el, s)
            ;['mouseenter', 'focusin'].forEach((ev) => on(el, ev, open))
            ;['mouseleave', 'focusout'].forEach((ev) => on(el, ev, close))
        on(window, 'scroll', reposition, true)
        on(window, 'resize', reposition)
        on(document, 'keydown', (e: KeyboardEvent) => e.key === 'Escape' && close())

        s.cleanup = () => { hide(el, s); ctrl.abort() }
        states.set(el, s)
    },
    updated(el, binding) {
        const s = states.get(el)
        if (!s) return
        s.opts = parse(binding)
        if (!s.tip) return
        if (s.opts.disabled || !s.opts.text) hide(el, s)
        else { s.tip.textContent = s.opts.text; place(el, s) }
    },
    beforeUnmount(el) {
        states.get(el)?.cleanup()
        states.delete(el)
    },
}

export default vTooltip