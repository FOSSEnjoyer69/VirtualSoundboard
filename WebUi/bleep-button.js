document.addEventListener("DOMContentLoaded", function () 
{
    button = document.querySelector('#bleep-button');
    if (!button)
    {
        console.error("Button element not found at #bleep-button");
        throw new Error("Button element not found");
    }

    async function sendSignal(state) {
    try 
    {
        await fetch('/api/play-beep', 
        {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ play: state })
        });
        } 
        catch (error) 
        {
            console.error("Failed to send signal:", error);
        }
    }
    button.addEventListener('mousedown', () => sendSignal(true));
    button.addEventListener('mouseup', () => sendSignal(false));
    button.addEventListener('mouseleave', () => sendSignal(false));
    button.addEventListener('touchstart', () => sendSignal(true));
    button.addEventListener('touchend', () => sendSignal(false));
});