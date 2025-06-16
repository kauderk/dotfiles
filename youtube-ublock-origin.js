youtube-ublock-origin.js text/javascript
(function () {
    'use strict'
    console.log('youtube-ublock-origin.js')
 
    if (is_index_page()) { return }
    function is_index_page() {
        return (
            location.pathname.startsWith('/results') || 
            location.pathname.startsWith('/@')
        )
    }
 
    const terms = [
        'expedition 33',
    ]
    const blacklist = terms.map(term => {
        const esc = term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
        return new RegExp(`\\b${esc}\\b`, 'i')
    })
 
    const ITEM_SELECTOR = 'ytd-rich-item-renderer' /*, ytd-rich-grid-media, ytd-video-renderer*/
    const TITLE_SELECTOR = '#video-title'
    const CHANNEL_SELECTOR = 'ytd-channel-name a'
 
 
    ;new MutationObserver(mutations => {
        for (const { addedNodes } of mutations) {
            for (const node of addedNodes) {
                if (!(node instanceof HTMLElement)) continue
                if (node.matches(ITEM_SELECTOR)) filter_item(node)
                else node.querySelectorAll && node.querySelectorAll(ITEM_SELECTOR).forEach(filter_item)
            }
        }
    }).observe(document.body, { childList: true, subtree: true })
 
    scan_page()
    function scan_page() {
        if (is_index_page()) {
            console.log('will not filter')
            return
        }
        document.querySelectorAll(ITEM_SELECTOR).forEach(filter_item)
    }
    function filter_item(item) {
        if (is_index_page()) { return }

        if (item.dataset.keywordBlocked) { return }
        item.dataset.keywordBlocked = '1'

        const linkEl = item.querySelector('a#video-title-link')
        const title = linkEl
            ? linkEl.getAttribute('aria-label').trim()
            : '‹no title›'
        console.log('› filter_item saw:', item.tagName, title)

        const channelEl = item.querySelector(CHANNEL_SELECTOR)
        const channel = channelEl ? channelEl.textContent.trim() : ''

        for (const re of blacklist) {
            if (re.test(title) || re.test(channel)) {
                console.log(
                    `Blocking video:\n` +
                    `    title:   "${title}"\n` +
                    `    channel: "${channel}"\n` +
                    `    matched:  ${re}`
                )
                item.style.display = 'none'
                return
            }
        }
    }
 
    window.addEventListener('yt-navigate-finish', scan_page)
})()