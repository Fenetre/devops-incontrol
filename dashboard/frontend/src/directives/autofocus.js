const FOCUSABLE = 'input, select, textarea, button, [tabindex]:not([tabindex="-1"])'

export const vAutofocus = {
  mounted(el, binding, vnode) {
    setTimeout(() => {
      // If used on a component that exposes an open() method (e.g. SelectMenu), call it
      const instance = vnode.component || el.__vueParentComponent
      if (instance?.exposed?.open) {
        instance.exposed.open()
        return
      }
      if (instance?.proxy?.open) {
        instance.proxy.open()
        return
      }
      const target = el.matches(FOCUSABLE) ? el : el.querySelector(FOCUSABLE)
      target?.focus()
    }, 50)
  }
}
