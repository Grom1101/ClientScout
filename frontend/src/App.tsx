import { Routes, Route } from 'react-router-dom';
import BottomNav from './components/BottomNav';
import HomePage from './pages/HomePage';
import SearchPage from './pages/SearchPage';
import SearchChatsPage from './pages/SearchChatsPage';
import MailingPage from './pages/MailingPage';
import MailingChatsPage from './pages/MailingChatsPage';

export default function App() {
  return (
    <div className="flex flex-col h-screen" style={{ maxWidth: 430, margin: '0 auto', backgroundColor: '#0B0E18' }}>
      <div className="flex-1 overflow-y-auto overflow-x-hidden">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/search" element={<SearchPage />} />
          <Route path="/search/chats" element={<SearchChatsPage />} />
          <Route path="/mailing" element={<MailingPage />} />
          <Route path="/mailing/chats" element={<MailingChatsPage />} />
        </Routes>
      </div>
      <BottomNav />
    </div>
  );
}
