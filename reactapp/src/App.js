import React, { useState } from 'react';
import axios from 'axios';
import './App.css';

function App() {
    const [file, setFile] = useState(null);
    const [scale, setScale] = useState(2.0);
    const [compression, setCompression] = useState(9);
    const [dpi, setDpi] = useState(500);
    const [uploading, setUploading] = useState(false);
    const [advanced, setAdvanced] = useState(false);
    const [advancedSettingsVisible, setAdvancedSettingsVisible] = useState(false);

    const handleFileChange = (e) => {
        setFile(e.target.files[0]);
    };

    const handleScaleChange = (e) => {
        setScale(e.target.value);
    };

    const handleCompressionChange = (e) => {
        setCompression(e.target.value);
    };

    const handleDpiChange = (e) => {
        setDpi(e.target.value);
    };

    const handleAdvanced = () => {
        setAdvanced(!advanced);
    };

    const handleUpload = async (e) => {
        e.preventDefault();

        if (!file) {
            alert('Please select a PDF file.');
            return;
        }

        const formData = new FormData();
        formData.append('file', file);
        formData.append('scale', scale.toString());
        formData.append('compression', compression.toString());
        formData.append('dpi', dpi.toString());

        setUploading(true);

        try {
            const response = await axios.post('https://pdf-splitter-kabacinski.azurewebsites.net/api/upload', formData, {
                responseType: 'blob',
            });

            const url = window.URL.createObjectURL(new Blob([response.data]));
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', 'Split_PDF.zip');
            document.body.appendChild(link);
            link.click();
        } catch (error) {
            alert('Error uploading file.');
            console.error(error);
        } finally {
            setUploading(false);
        }
    };

    return (
        <div className="App">
            <header className="App-header">
                <h1>PDF Splitter</h1>
                <p>Upload a PDF file to split it into multiple PDFs, one per page.</p>

                <form onSubmit={handleUpload}>
                    <div>
                        <input id="fileUpload" className="fileInput" type="file" accept=".pdf" onChange={handleFileChange} />
                        <label htmlFor="fileUpload" className="uploadFileButton">Select File</label>
                    </div>
                    <div>
                        <button className="uploadButton" type="submit" disabled={uploading || !file}>
                            {uploading ? 'Uploading...' : 'Upload'}
                        </button>
                    </div>

                    <div className="separator"></div>

                    <div className="advanced-settings">
                        <button className="advancedButton" type="button" onClick={() => setAdvancedSettingsVisible(!advancedSettingsVisible)}>
                            Advanced
                        </button>
                        <div className={`slider ${advancedSettingsVisible ? 'slider-visible' : 'slider-hidden'}`}>
                            <label className="advancedLabel">
                                Scale: {parseFloat(scale).toFixed(1)}
                                <input type="range" min="0.1" max="5" step="0.1" value={scale} onChange={handleScaleChange} style={{ width: '100%' }} />
                                Compression: {parseFloat(compression).toFixed(0)}
                                <input type="range" min="1" max="9" step="1" value={compression} onChange={handleCompressionChange} style={{ width: '100%' }} />
                                DPI: {parseFloat(dpi).toFixed(0)}
                                <input type="range" min="100" max="900" step="100" value={dpi} onChange={handleDpiChange} style={{ width: '100%' }} />
                            </label>
                        </div>
                    </div>
                </form>
            </header>
        </div>
    );
}

export default App;
