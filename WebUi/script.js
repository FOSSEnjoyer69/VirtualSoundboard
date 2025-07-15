
        window.addEventListener('DOMContentLoaded', (event) => 
        {
            const micDropdown = document.getElementById('mic-dropdown');
            micDropdown.addEventListener('change', (e) => {
                setMicrophoneDevice(e.target.value);
            });

            const outputDropdown = document.getElementById('output-dropdown');
            outputDropdown.addEventListener('change', (e) => {
                setOutputDevice(e.target.value);
            });

            const dropZone = document.getElementById('grid-container');

            // Prevent default behavior (Prevent file from being opened)
            ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventType => {
                dropZone.addEventListener(eventType, (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                });
            });

            // Highlight drop area when dragging over it
            ['dragenter', 'dragover'].forEach(eventType => {
                dropZone.addEventListener(eventType, () => {
                    dropZone.classList.add('dragover');
                });
            });

            ['dragleave', 'drop'].forEach(eventType => {
                dropZone.addEventListener(eventType, () => {
                    dropZone.classList.remove('dragover');
                });
            });

            // Handle dropped files
            dropZone.addEventListener('drop', (e) => {
                const files = e.dataTransfer.files;
                for (const file of files)
                {
                    addSound(file);
                }
            });


            function addSound(file)
            {
                uploadSound(file);

                const soundElement = document.createElement('button');

                soundElement.classList.add("grid-item");
                soundElement.onclick = () => sendPlaySoundRequest(file.name);
                soundElement.innerText = file.name;
                soundElement.id = file.name;

                dropZone.appendChild(soundElement);
            }

            fetch("/api/get-sound-ids")
                .then(response => response.json())
                .then(data => 
                {
                    const sounds = data;
                    for (let i = 0; i < sounds.length; i++) 
                    {
                        const sound = sounds[i];

                        const soundElement = document.createElement('button');

                        soundElement.classList.add("grid-item");
                        soundElement.onclick = () => sendPlaySoundRequest(sound);
                        soundElement.innerText = sound;
                        soundElement.id = sound;

                        dropZone.appendChild(soundElement);
                    }
                });

            getMicrophoneNames();
            getOutputNames();
        });

        async function getMicrophoneNames() 
        {
            const url = '/api/input-devices';
            try 
            {
                const response = await fetch(url);
                if (response.ok) 
                {
                    const microphones = await response.json();
                    const micDropdown = document.getElementById('mic-dropdown');
                    microphones.forEach(mic => {
                        if (mic && mic.trim() !== "") {
                            const option = document.createElement('option');
                            option.value = mic;
                            option.textContent = mic;
                            micDropdown.appendChild(option);
                        }
                    });
                } 
                else 
                {
                    console.error('Failed to fetch microphone names');
                }
            } 
            catch (error) 
            {
                console.error('Error fetching microphone names:', error);
            }    
        }

        async function getOutputNames() 
        {
            const url = '/api/output-devices';
            try 
            {
                const response = await fetch(url);
                if (response.ok) 
                {
                    const microphones = await response.json();
                    const micDropdown = document.getElementById('output-dropdown');
                    microphones.forEach(mic => {
                        if (mic && mic.trim() !== "") {
                            const option = document.createElement('option');
                            option.value = mic;
                            option.textContent = mic;
                            micDropdown.appendChild(option);
                        }
                    });
                } 
                else 
                {
                    console.error('Failed to fetch microphone names');
                }
            } 
            catch (error) 
            {
                console.error('Error fetching microphone names:', error);
            }    
        }
        async function setMicrophoneDevice(deviceName) {
            const url = '/api/set-input-device';
            try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                'Content-Type': 'application/json'
                },
                body: JSON.stringify({ device: deviceName })
            });
            if (!response.ok) {
                console.error('Failed to set microphone device');
            }
            } catch (error) {
            console.error('Error setting microphone device:', error);
            }
        }        
        async function setOutputDevice(deviceName) {
            const url = '/api/set-output-device';
            try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                'Content-Type': 'application/json'
                },
                body: JSON.stringify({ device: deviceName })
            });
            if (!response.ok) {
                console.error('Failed to set microphone device');
            }
            } catch (error) {
            console.error('Error setting microphone device:', error);
            }
        }

        async function uploadSound(file) 
        {
            const url = '/api/upload-sound';
            
            const formData = new FormData();
            formData.append('file', file, file.name); 

            try 
            {
                const response = await fetch(url, 
                {
                    method: 'POST',
                    body: formData
                    });

                    if (response.ok) {
                        const result = await response.text();
                        console.log("sound uploaded");
                    } else {
                        alert('File upload failed');
                    }
                } catch (error) {
                    console.error('Error during upload:', error);
                    alert('An error occurred while uploading the file.');
                }
            
        }

        async function sendPlaySoundRequest(soundId) 
        {
            const url = '/api/play-sound';
            
            const response = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "text/plain"
                },
                body: soundId
            });
        }

        function generateUUID() 
        {
            return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                const r = (Math.random() * 16) | 0,
                      v = c === 'x' ? r : (r & 0x3) | 0x8;
                return v.toString(16);
            });
        }