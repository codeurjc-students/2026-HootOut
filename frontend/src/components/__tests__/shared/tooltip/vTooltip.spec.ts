import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, withDirectives, type PropType } from 'vue'
import { vTooltip, type TooltipValue } from '@/components/shared/tooltip/vTooltip'

// ---------- helpers ----------

const DEFAULT_DELAY = 67
const VIEWPORT = { width: 1000, height: 800 }

// Host component that applies the directive the same way as
// <button v-tooltip:[arg]="value">Hover me</button>
const Host = defineComponent({
  props: {
    value: { type: [String, Object] as PropType<TooltipValue>, default: undefined },
    arg: { type: String, default: undefined },
  },
  setup(props) {
    return () => withDirectives(h('button', 'Hover me'), [[vTooltip, props.value, props.arg]])
  },
})

const wrappers: VueWrapper[] = []

function mountTooltip(value?: TooltipValue, arg?: string) {
  const wrapper = mount(Host, { props: { value, arg }, attachTo: document.body })
  wrappers.push(wrapper)
  return wrapper
}

const tooltip = () => document.querySelector<HTMLElement>('.v-tooltip')
const allTooltips = () => document.querySelectorAll('.v-tooltip')

async function hover(wrapper: VueWrapper, wait = DEFAULT_DELAY) {
  await wrapper.trigger('mouseenter')
  vi.advanceTimersByTime(wait)
}

async function focus(wrapper: VueWrapper, wait = DEFAULT_DELAY) {
  await wrapper.trigger('focusin')
  vi.advanceTimersByTime(wait)
}

function pressKey(key: string) {
  document.dispatchEvent(new KeyboardEvent('keydown', { key }))
}

function rect(left: number, top: number, width: number, height: number): DOMRect {
  return {
    left,
    top,
    width,
    height,
    right: left + width,
    bottom: top + height,
    x: left,
    y: top,
    toJSON: () => ({}),
  } as DOMRect
}

// jsdom has no layout engine: give fake sizes to the host element and the tooltip
function mockRects(target: DOMRect, tip = { width: 80, height: 20 }) {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
    this: HTMLElement
  ) {
    return this.classList.contains('v-tooltip') ? rect(0, 0, tip.width, tip.height) : target
  })
}

const position = () => ({ left: tooltip()!.style.left, top: tooltip()!.style.top })

// ---------- tests ----------

describe('vTooltip', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal('innerWidth', VIEWPORT.width)
    vi.stubGlobal('innerHeight', VIEWPORT.height)
  })

  afterEach(() => {
    wrappers.splice(0).forEach((wrapper) => wrapper.unmount())
    document.body.innerHTML = ''
    vi.useRealTimers()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  describe('showing', () => {
    it('does not show the tooltip on mount', () => {
      mountTooltip('Hello')

      expect(tooltip()).toBeNull()
    })

    it('shows the tooltip after the default delay on mouseenter', async () => {
      const wrapper = mountTooltip('Hello')

      await hover(wrapper, DEFAULT_DELAY - 1)
      expect(tooltip()).toBeNull()

      vi.advanceTimersByTime(1)
      expect(tooltip()).not.toBeNull()
    })

    it('shows the tooltip on focusin', async () => {
      const wrapper = mountTooltip('Hello')

      await focus(wrapper)

      expect(tooltip()).not.toBeNull()
    })

    it('appends the tooltip to the body with its text, class and role', async () => {
      const wrapper = mountTooltip('Hello')

      await hover(wrapper)

      expect(tooltip()!.parentElement).toBe(document.body)
      expect(tooltip()!.textContent).toBe('Hello')
      expect(tooltip()!.getAttribute('role')).toBe('tooltip')
    })

    it('links the element to the tooltip with aria-describedby', async () => {
      const wrapper = mountTooltip('Hello')

      await hover(wrapper)

      expect(tooltip()!.id).toMatch(/^v-tooltip-/)
      expect(wrapper.attributes('aria-describedby')).toBe(tooltip()!.id)
    })

    it('uses the text from an options object', async () => {
      const wrapper = mountTooltip({ text: 'From options' })

      await hover(wrapper)

      expect(tooltip()!.textContent).toBe('From options')
    })

    it('uses a custom delay', async () => {
      const wrapper = mountTooltip({ text: 'Hello', delay: 500 })

      await hover(wrapper, 499)
      expect(tooltip()).toBeNull()

      vi.advanceTimersByTime(1)
      expect(tooltip()).not.toBeNull()
    })

    it.each<[string, TooltipValue | undefined]>([
      ['the value is undefined', undefined],
      ['the text is empty', ''],
      ['the options have no text', { delay: 10 }],
      ['the tooltip is disabled', { text: 'Hello', disabled: true }],
    ])('does not show the tooltip when %s', async (_, value) => {
      const wrapper = mountTooltip(value)

      await hover(wrapper)

      expect(tooltip()).toBeNull()
      expect(wrapper.attributes('aria-describedby')).toBeUndefined()
    })

    it('creates a single tooltip when entering several times', async () => {
      const wrapper = mountTooltip('Hello')

      await hover(wrapper)
      await focus(wrapper)
      await hover(wrapper)

      expect(allTooltips()).toHaveLength(1)
    })

    it('gives each element its own tooltip', async () => {
      const first = mountTooltip('First')
      const second = mountTooltip('Second')

      await hover(first)
      await hover(second)

      const tips = Array.from(allTooltips())
      expect(tips.map((tip) => tip.textContent)).toEqual(['First', 'Second'])
      expect(first.attributes('aria-describedby')).not.toBe(second.attributes('aria-describedby'))
    })
  })

  describe('hiding', () => {
    it('hides the tooltip on mouseleave', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      await wrapper.trigger('mouseleave')

      expect(tooltip()).toBeNull()
      expect(wrapper.attributes('aria-describedby')).toBeUndefined()
    })

    it('hides the tooltip on focusout', async () => {
      const wrapper = mountTooltip('Hello')
      await focus(wrapper)

      await wrapper.trigger('focusout')

      expect(tooltip()).toBeNull()
    })

    it('hides the tooltip when Escape is pressed', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      pressKey('Escape')

      expect(tooltip()).toBeNull()
      expect(wrapper.attributes('aria-describedby')).toBeUndefined()
    })

    it('keeps the tooltip when another key is pressed', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      pressKey('Enter')

      expect(tooltip()).not.toBeNull()
    })

    it('never shows the tooltip if the mouse leaves before the delay', async () => {
      const wrapper = mountTooltip('Hello')

      await hover(wrapper, DEFAULT_DELAY / 2)
      await wrapper.trigger('mouseleave')
      vi.advanceTimersByTime(DEFAULT_DELAY)

      expect(tooltip()).toBeNull()
    })

    it('shows the tooltip again on the next hover', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)
      await wrapper.trigger('mouseleave')

      await hover(wrapper)

      expect(allTooltips()).toHaveLength(1)
      expect(wrapper.attributes('aria-describedby')).toBe(tooltip()!.id)
    })
  })

  describe('hover and focus together', () => {
    it('keeps the tooltip when the mouse leaves but the element is still focused', async () => {
      const wrapper = mountTooltip('Hello')
      await focus(wrapper)
      await hover(wrapper)

      await wrapper.trigger('mouseleave')

      expect(tooltip()).not.toBeNull()
    })

    it('keeps the tooltip when focus is lost but the mouse is still over the element', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)
      await focus(wrapper)

      await wrapper.trigger('focusout')

      expect(tooltip()).not.toBeNull()
    })

    it('hides the tooltip once the element is neither hovered nor focused', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)
      await focus(wrapper)

      await wrapper.trigger('mouseleave')
      await wrapper.trigger('focusout')

      expect(tooltip()).toBeNull()
    })

    it('hides the tooltip on Escape even while hovered and focused', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)
      await focus(wrapper)

      pressKey('Escape')

      expect(tooltip()).toBeNull()
    })
  })

  describe('updating the value', () => {
    it('updates the text of a visible tooltip', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      await wrapper.setProps({ value: 'Bye' })

      expect(tooltip()!.textContent).toBe('Bye')
      expect(allTooltips()).toHaveLength(1)
    })

    it('hides a visible tooltip when it becomes disabled', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      await wrapper.setProps({ value: { text: 'Hello', disabled: true } })

      expect(tooltip()).toBeNull()
      expect(wrapper.attributes('aria-describedby')).toBeUndefined()
    })

    it('hides a visible tooltip when its text becomes empty', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      await wrapper.setProps({ value: '' })

      expect(tooltip()).toBeNull()
    })

    it('cancels a pending tooltip when it becomes disabled during the delay', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper, DEFAULT_DELAY / 2)

      await wrapper.setProps({ value: { text: 'Hello', disabled: true } })
      vi.advanceTimersByTime(DEFAULT_DELAY)

      expect(tooltip()).toBeNull()
    })

    it('cancels a pending tooltip when its text becomes empty during the delay', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper, DEFAULT_DELAY / 2)

      await wrapper.setProps({ value: '' })
      vi.advanceTimersByTime(DEFAULT_DELAY)

      expect(tooltip()).toBeNull()
    })

    it('shows the latest text when it changes during the delay', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper, DEFAULT_DELAY / 2)

      await wrapper.setProps({ value: 'Bye' })
      vi.advanceTimersByTime(DEFAULT_DELAY)

      expect(tooltip()!.textContent).toBe('Bye')
    })

    it('repositions a visible tooltip when the placement changes', async () => {
      mockRects(rect(400, 300, 100, 40))
      const wrapper = mountTooltip('Hello', 'top')
      await hover(wrapper)

      await wrapper.setProps({ arg: 'bottom' })

      expect(position()).toEqual({ left: '410px', top: '348px' })
    })
  })

  describe('placement', () => {
    // Host element: left 400, top 300, 100x40 (right 500, bottom 340). Tooltip: 80x20. Gap: 8.
    beforeEach(() => {
      mockRects(rect(400, 300, 100, 40))
    })

    it.each([
      { placement: 'top', expected: { left: '410px', top: '272px' } },
      { placement: 'bottom', expected: { left: '410px', top: '348px' } },
      { placement: 'left', expected: { left: '312px', top: '310px' } },
      { placement: 'right', expected: { left: '508px', top: '310px' } },
    ])('places the tooltip on the $placement of the element', async ({ placement, expected }) => {
      const wrapper = mountTooltip('Hello', placement)

      await hover(wrapper)

      expect(position()).toEqual(expected)
    })

    it('places the tooltip on top by default', async () => {
      const wrapper = mountTooltip('Hello')

      await hover(wrapper)

      expect(position()).toEqual({ left: '410px', top: '272px' })
    })

    it('uses the placement from the options object', async () => {
      const wrapper = mountTooltip({ text: 'Hello', placement: 'right' })

      await hover(wrapper)

      expect(position()).toEqual({ left: '508px', top: '310px' })
    })

    it('gives priority to the directive argument over the options placement', async () => {
      const wrapper = mountTooltip({ text: 'Hello', placement: 'right' }, 'bottom')

      await hover(wrapper)

      expect(position()).toEqual({ left: '410px', top: '348px' })
    })

    it('falls back to top for an unknown argument', async () => {
      const wrapper = mountTooltip('Hello', 'foo')

      await hover(wrapper)

      expect(position()).toEqual({ left: '410px', top: '272px' })
    })

    it('falls back to top for an unknown placement in the options', async () => {
      const wrapper = mountTooltip({ text: 'Hello', placement: 'center' as never })

      await hover(wrapper)

      expect(position()).toEqual({ left: '410px', top: '272px' })
    })
  })

  describe('fitting in the viewport', () => {
    it('flips to the opposite side when the preferred one does not fit', async () => {
      // Element near the top edge: there is no room above it
      mockRects(rect(400, 10, 100, 40))
      const wrapper = mountTooltip('Hello', 'top')

      await hover(wrapper)

      expect(position().top).toBe('58px') // below: 10 + 40 + 8
    })

    it('flips from left to right when there is no room on the left', async () => {
      mockRects(rect(20, 300, 100, 40))
      const wrapper = mountTooltip('Hello', 'left')

      await hover(wrapper)

      expect(position().left).toBe('128px') // right: 20 + 100 + 8
    })

    it('keeps the preferred side and clamps it when neither side fits', async () => {
      // Element as tall as the viewport: no room above or below
      mockRects(rect(400, 5, 100, 790))
      const wrapper = mountTooltip('Hello', 'top')

      await hover(wrapper)

      expect(position().top).toBe('4px')
    })

    it('keeps the tooltip at least 4px from the left edge', async () => {
      mockRects(rect(0, 300, 20, 40))
      const wrapper = mountTooltip('Hello', 'top')

      await hover(wrapper)

      expect(position().left).toBe('4px')
    })

    it('keeps the tooltip at least 4px from the right edge', async () => {
      mockRects(rect(980, 300, 20, 40))
      const wrapper = mountTooltip('Hello', 'top')

      await hover(wrapper)

      expect(position().left).toBe('916px') // 1000 - 80 - 4
    })
  })

  describe('repositioning', () => {
    it.each([
      ['the window scrolls', () => window.dispatchEvent(new Event('scroll'))],
      ['the window is resized', () => window.dispatchEvent(new Event('resize'))],
      [
        'a scrollable container scrolls',
        () => {
          const container = document.createElement('div')
          document.body.appendChild(container)
          container.dispatchEvent(new Event('scroll')) // scroll does not bubble: needs the capture listener
        },
      ],
    ])('moves the tooltip when %s', async (_, dispatch) => {
      mockRects(rect(400, 300, 100, 40))
      const wrapper = mountTooltip('Hello', 'top')
      await hover(wrapper)

      mockRects(rect(400, 200, 100, 40))
      dispatch()

      expect(position()).toEqual({ left: '410px', top: '172px' })
    })

    it('does nothing on scroll when the tooltip is hidden', () => {
      mountTooltip('Hello')

      expect(() => window.dispatchEvent(new Event('scroll'))).not.toThrow()
      expect(tooltip()).toBeNull()
    })
  })

  describe('unmount', () => {
    it('removes a visible tooltip', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper)

      wrapper.unmount()

      expect(tooltip()).toBeNull()
    })

    it('cancels a pending tooltip', async () => {
      const wrapper = mountTooltip('Hello')
      await hover(wrapper, DEFAULT_DELAY / 2)

      wrapper.unmount()
      vi.advanceTimersByTime(DEFAULT_DELAY)

      expect(tooltip()).toBeNull()
    })

    it('removes the event listeners', async () => {
      const wrapper = mountTooltip('Hello')
      const button = wrapper.element as HTMLElement
      const placeSpy = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect')

      wrapper.unmount()
      button.dispatchEvent(new Event('mouseenter'))
      button.dispatchEvent(new Event('focusin'))
      vi.advanceTimersByTime(DEFAULT_DELAY)
      window.dispatchEvent(new Event('resize'))

      expect(tooltip()).toBeNull()
      expect(placeSpy).not.toHaveBeenCalled()
    })
  })
})
