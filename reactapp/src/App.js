import React, { useState } from 'react';
import axios from 'axios';
import './App.css';

function App() {
    const [file, setFile] = useState(null);
    const [uploading, setUploading] = useState(false);

    const handleFileChange = (e) => {
        setFile(e.target.files[0]);
    };

    const handleUpload = async (e) => {
        e.preventDefault();

        if (!file) {
            alert('Please select a PDF file.');
            return;
        }

        const formData = new FormData();
        formData.append('file', file);

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
                    <input type="file" accept=".pdf" onChange={handleFileChange} />
                    <button type="submit" disabled={uploading}>
                        {uploading ? 'Uploading...' : 'Upload and Split'}
                    </button>
                </form>
            </header>
        </div>
    );
}

export default App;